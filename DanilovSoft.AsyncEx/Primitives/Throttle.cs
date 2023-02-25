using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx;

public sealed class Throttle<TState> : IDisposable, IAsyncDisposable
{
    private readonly object _invokeLock = new();
    private readonly object _timerObj = new();
    private Action<TState>? _callback;
    /// <summary>
    /// Чтение и запись только внутри блокировки _invokeLock.
    /// </summary>
    [AllowNull] private TState _state;
    private Timer? _timer;
    private volatile bool _disposed;
    private volatile bool _scheduled;

    /// <exception cref="ArgumentNullException"/>
    public Throttle(Action<TState> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        _callback = callback;
        _timer = new Timer(OnTimer, null, -1, -1);
    }

    /// <param name="delay">Задержка срабатывания. Укажите TimeSpan.Zero что-бы выполнить колбэк немедленно.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="ObjectDisposedException"/>
    public void Invoke(TimeSpan delay, TState state) => Invoke((int)delay.TotalMilliseconds, state);

    /// <param name="delayMsec">Задержка срабатывания в миллисекундах. Укажите ноль (0) что-бы выполнить колбэк немедленно.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="ObjectDisposedException"/>
    public void Invoke(int delayMsec, TState state)
    {
        if (delayMsec < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMsec));
        }
        
        lock (_invokeLock)
        {
            CheckDisposed();

            _state = state;

            if (!_scheduled)
            {
                _scheduled = true;
                _timer.Change(delayMsec, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// Блокирует поток для ожидания завершения колбэка.
    /// </summary>
    public void Dispose()
    {
        var completed = DisposeCore();

        if (!completed)
        {
            // Гарантируем завершение колбэка.
            var lockTaken = false;
            try
            {
                Monitor.Enter(_timerObj, ref lockTaken);
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_timerObj);
                }
            }
        }
    }

    /// <summary>
    /// Использует поток из пула для ожидания завершения колбэка.
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        var completed = DisposeCore();

        if (completed)
        {
            return default;
        }

        return WaitForCallbackToCompleteAsync();
    }

    /// <returns>True если вызов колбэка гарантированно предотвращён.</returns>
    private bool DisposeCore()
    {
        lock (_invokeLock)
        {
            if (!_disposed)
            {
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
                _callback = null;

                var lockTaken = false;
                try
                {
                    Monitor.TryEnter(_timerObj, ref lockTaken);
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
                        Monitor.Exit(_timerObj);
                    }
                }
            }
            else
            {
                return true;
            }
        }
    }

    private void OnTimer(object? _)
    {
        TState state;
        Action<TState>? callback;

        lock (_invokeLock)
        {
            state = _state;
            callback = _callback;
            _state = default!;
        }

        if (callback != null)
        {
            lock (_timerObj)
            {
                if (_scheduled)
                {
                    callback.Invoke(state);
                }
            }
        }

        // Разрешить следующий запуск таймера.
        _scheduled = false;
    }

    [MemberNotNull(nameof(_timer))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckDisposed()
    {
        if (!_disposed)
        {
            Debug.Assert(_timer != null);
            return;
        }

        ThrowHelper.ThrowObjectDisposed<Throttle<TState>>();
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
