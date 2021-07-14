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
        private Task _task = Task.CompletedTask;
        private bool _disposed;
        /// <summary>
        /// Чтение и запись только в блокировке _invokeObj.
        /// </summary>
        [AllowNull] private T _arg;
        

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

            if (delay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            _delay = delay;
        }

        /// <exception cref="ObjectDisposedException"/>
        public void Invoke(T arg)
        {
            lock (_invokeObj)
            {
                CheckDisposed();

                _arg = arg;

                if (_task.IsCompleted)
                {
                    _task = WaitAsync();
                }
            }
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

                Debug.Assert(Volatile.Read(ref _callback) == null);
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

        private async Task WaitAsync()
        {
            await Task.Delay(_delay).ConfigureAwait(false);

            T arg;
            lock (_invokeObj)
            {
                arg = _arg;
                _arg = default;
            }

            if (_callback != null)
            {
                _callback.Invoke(arg);
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

        private void CheckDisposed()
        {
            if (_disposed)
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
                while (!state._task.IsCompleted)
                {
                    await Task.Yield();
                }
            }, this, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).Unwrap());
        }
    }
}
