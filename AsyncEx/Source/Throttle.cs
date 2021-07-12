using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public sealed class Throttle<T> : IDisposable, IAsyncDisposable
    {
        private readonly object _invokeObj = new();
        private readonly object _timerObj = new();
        private readonly TimeSpan _delay;
        private Action<T>? _callback;
        private Timer? _timer;
        private bool _scheduled;
        /// <summary>
        /// Чтение и запись только в блокировке _invokeObj.
        /// </summary>
        [AllowNull] private T _arg;
        private bool _completed;
        private bool _disposed;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delay">Задержка в миллисекундах.</param>
        public Throttle(Action<T> callback, int delay) : this(callback, TimeSpan.FromMilliseconds(delay))
        {
        }

        public Throttle(Action<T> callback, TimeSpan delay)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));

            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            _delay = delay;
            _timer = new Timer(static s => ((Throttle<T>)s!).OnTimer(), this, -1, -1);
        }

        public void Dispose()
        {
            bool completed = DisposeCore();

            if (!completed)
            {
                // Гарантируем завершение колбэка по выходу из Dispose.
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

                Debug.Assert(Volatile.Read(ref _completed));
            }
        }

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

                if (!_scheduled)
                {
                    _scheduled = true;
                    _timer.Change(_delay, Timeout.InfiniteTimeSpan);
                }
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

                    bool lockTaken = false;
                    try
                    {
                        Monitor.TryEnter(_timerObj, ref lockTaken);
                        if (lockTaken)
                        {
                            _callback = null;
                            _completed = true;
                            return true;
                        }
                        else
                        {
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
            lock (_invokeObj)
            {
                arg = _arg;
                _arg = default;
                _scheduled = false;
            }

            lock (_timerObj)
            {
                _callback?.Invoke(arg);

                if (Volatile.Read(ref _disposed))
                {
                    Volatile.Write(ref _completed, true);
                }
            }
        }

        [MemberNotNull(nameof(_timer))]
        private void CheckDisposed()
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposed<Throttle<T>>();
            }

            Debug.Assert(_timer != null);
        }

        /// <summary>
        /// Асинхронно ожидает однократное завершение колбэка или проверяет что колбэк не запущен.
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
                while (!Volatile.Read(ref state._completed))
                {
                    await Task.Yield();
                }
            }, this, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
        }
    }
}
