using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    // TODO незакончена синхронизация!
    internal sealed class Debounce<T> : IDisposable, IAsyncDisposable
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
        private volatile int _scheduled;

        public Debounce(Action<T> callback, TimeSpan delay) : this(callback, (long)delay.TotalMilliseconds)
        {
        }

        /// <param name="delay">Задержка в миллисекундах.</param>
        public Debounce(Action<T> callback, long delay)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));

            if (delay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            _delayMsec = delay;
            _timer = new Timer(static s => ((Debounce<T>)s!).OnTimer(), this, -1, -1);
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

        /// <exception cref="ObjectDisposedException"/>
        public void Invoke(T arg)
        {
            lock (_invokeObj)
            {
                CheckDisposed();

                _arg = arg;

                if (_scheduled != 0)
                {
                    _timer.Change(_delayMsec, Timeout.Infinite); // Перезапуск таймера.
                }
                else
                {
                    _scheduled = 1;
                    _timer.Change(_delayMsec, Timeout.Infinite);
                }
            }
        }

        private void OnTimer()
        {
            T arg;
            //Action<T>? callback;
            lock (_invokeObj)
            {
                if (_scheduled == 1)
                {
                    _scheduled = 2;

                    arg = _arg;
                    //callback = _callback;
                    _arg = default;
                }
                else
                {
                    return; // Другой таймер нас обогнал.
                }
            }


            lock (_timerObj)
            {
                if (_callback != null)
                {
                    //if (_scheduled == 2)
                    {
                        _callback.Invoke(arg);
                    }
                }
            }

            // Разрешить следующий запуск таймера.
            _scheduled = 0;
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

                    bool lockTaken = false;
                    try
                    {
                        Monitor.TryEnter(_timerObj, ref lockTaken);
                        if (lockTaken)
                        {
                            _callback = null;
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
                ThrowHelper.ThrowObjectDisposed<Debounce<T>>();
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
                var state = (Debounce<T>)s!;
                while (state._scheduled != 0)
                {
                    await Task.Yield();
                }
            }, this, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
        }
    }
}
