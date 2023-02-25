using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx;

public sealed class Throttle<TState> : IDisposable, IAsyncDisposable
{
    private readonly object _scheduleLock = new();
    private readonly object _callbackLock = new();
    /// <summary>
    /// Чтение и запись только внутри блокировки _invokeLock.
    /// </summary>
    [AllowNull] private TState _state;
    private Timer? _timer;
    private volatile bool _scheduled;

    /// <exception cref="ArgumentNullException"/>
    public Throttle(Action<TState> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        _timer = new Timer(OnTimer, callback, -1, -1);
    }

    public bool IsScheduled => _scheduled;

    /// <summary>
    /// Блокирует поток для ожидания завершения колбэка.
    /// </summary>
    public void Dispose()
    {
        var callbackCompleted = DisposeCore();

        if (!callbackCompleted)
        {
            // Гарантируем завершение колбэка.
            lock (_callbackLock) { }
        }
    }

    /// <summary>
    /// Использует поток из пула для ожидания завершения колбэка.
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        var callbackCompleted = DisposeCore();

        if (callbackCompleted)
        {
            return default;
        }

        return WaitForCallbackToCompleteAsync();
    }

    /// <param name="delay">Задержка срабатывания. Укажите TimeSpan.Zero что-бы выполнить колбэк немедленно.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="ObjectDisposedException"/>
    public void Schedule(TimeSpan delay, TState state) => Schedule((int)delay.TotalMilliseconds, state);

    /// <param name="delayMsec">Задержка срабатывания в миллисекундах. Укажите ноль (0) что-бы выполнить колбэк немедленно.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="ObjectDisposedException"/>
    public void Schedule(int delayMsec, TState state)
    {
        if (delayMsec < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMsec));
        }
        
        lock (_scheduleLock)
        {
            if (_timer is null)
            {
                ThrowHelper.ThrowObjectDisposed<Throttle<TState>>();
            }

            _state = state;

            if (_scheduled)
            {
                return;
            }

            _scheduled = true;
            _timer.Change(delayMsec, Timeout.Infinite);
        }
    }

    /// <returns>True если вызов колбэка гарантированно предотвращён.</returns>
    private bool DisposeCore()
    {
        lock (_scheduleLock)
        {
            if (_timer is null)
            {
                return true;
            }

            _timer.Dispose();
            _timer = null;

            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_callbackLock, ref lockTaken);
                if (lockTaken)
                {
                    _scheduled = false;
                    return true;
                }
                else
                {
                    // Таймер держит блокировку, значит выполняет колбэк.
                    return false;
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_callbackLock);
                }
            }
        }
    }

    private void OnTimer(object? timerState)
    {
        TState state;

        lock (_scheduleLock)
        {
            state = _state;
            _state = default!;
        }

        lock (_callbackLock)
        {
            if (_scheduled)
            {
                ((Action<TState>)timerState!).Invoke(state);
            }
        }

        // Разрешить следующий запуск таймера.
        _scheduled = false;
    }

    /// <summary>
    /// Асинхронно ожидает однократное завершение колбэка.
    /// </summary>
    private ValueTask WaitForCallbackToCompleteAsync()
    {
        // The specified callback is actually running: queue an async loop that'll poll for the currently executing
        // callback to complete. While such polling isn't ideal, we expect this to be a rare case (disposing while
        // the associated callback is running), and brief when it happens (so the polling will be minimal), and making
        // this work with a callback mechanism will add additional cost to other more common cases.
        return new ValueTask(Task.Factory.StartNew(async s =>
        {
            while (_scheduled)
            {
                await Task.Yield();
            }
        }, null, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
    }
}
