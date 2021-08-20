using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public sealed class Throttle<T> : IDisposable, IAsyncDisposable
    {
        private readonly object _invokeObj = new();
        private readonly object _timerObj = new();
        private readonly long _delayMsec;
        private Timer? _timer;
        /// <summary>
        /// Чтение и запись только в блокировке _invokeObj.
        /// </summary>
        [AllowNull] private T _arg;
        private Action<T>? _callback;
        private volatile bool _disposed;
        private volatile bool _scheduled;

        public Throttle(Action<T> callback, TimeSpan delay) : this(callback, (long)delay.TotalMilliseconds)
        {
        }

        /// <param name="delay">Задержка в миллисекундах.</param>
        public Throttle(Action<T> callback, long delay)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));

            if (delay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            _delayMsec = delay;
            _timer = new Timer(static s => ((Throttle<T>)s!).OnTimer(), this, -1, -1);
        }

        /// <exception cref="ObjectDisposedException"/>
        public void Invoke(T arg)
        {
            lock (_invokeObj)
            {
                CheckDisposed();

                _arg = arg;

                if (!_scheduled)
                {
                    _scheduled = true;
                    _timer.Change(_delayMsec, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Блокирует поток для ожидания завершения колбэка.
        /// </summary>
        public void Dispose()
        {
            bool completed = DisposeCore();

            if (!completed)
            {
                // Гарантируем завершение колбэка.
                bool lockTaken = false;
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
            bool completed = DisposeCore();

            if (completed)
            {
                return default;
            }
            else
            {
                return WaitForCallbackToCompleteAsync();
            }
        }

        /// <returns>True если вызов колбэка гарантированно предотвращён.</returns>
        private bool DisposeCore()
        {
            lock (_invokeObj)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _timer?.Dispose();
                    _timer = null;
                    _callback = null;

                    bool lockTaken = false;
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

        private void OnTimer()
        {
            T arg;
            Action<T>? callback;
            lock (_invokeObj)
            {
                arg = _arg;
                callback = _callback;
                _arg = default;
            }

            if (callback != null)
            {
                lock (_timerObj)
                {
                    if (_scheduled)
                    {
                        callback.Invoke(arg);
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
            }
            else
            {
                ThrowHelper.ThrowObjectDisposed<Throttle<T>>();
            }
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
            return new ValueTask(Task.Factory.StartNew(static async s =>
            {
                var state = (Throttle<T>)s!;
                while (state._scheduled)
                {
                    await Task.Yield();
                }
            }, this, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
        }
    }
}
