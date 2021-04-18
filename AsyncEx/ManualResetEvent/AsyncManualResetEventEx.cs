using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    [DebuggerDisplay("IsSet = {IsSet}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class AsyncManualResetEventEx
    {
        private readonly Queue<QueueAwaiter> _awaiters = new();
        private bool _set;

        public AsyncManualResetEventEx() : this(initialState: false)
        {

        }

        public AsyncManualResetEventEx(bool initialState)
        {
            _set = initialState;
        }

        public bool IsSet => Volatile.Read(ref _set);

        public void Set()
        {
            // Должны отпустить все ожидающие Task и установить сигнальное состояние.
            lock (_awaiters)
            {
                _set = true;
                while (_awaiters.TryDequeue(out var awaiter))
                {
                    awaiter.TrySet();
                }
            }
        }

        public void Reset()
        {
            lock (_awaiters)
            {
                _set = false;
            }
        }

        [DebuggerStepThrough]
        public Task WaitAsync()
        {
            return WaitAsync(Timeout.Infinite, CancellationToken.None);
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        [DebuggerStepThrough]
        public Task<bool> WaitAsync(int millisecondsTimeout)
        {
            return WaitAsync(millisecondsTimeout, CancellationToken.None);
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        [DebuggerStepThrough]
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitAsync(timeout, CancellationToken.None);
        }
        
        /// <exception cref="OperationCanceledException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        [DebuggerStepThrough]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(timeout));
            }

            return WaitAsync((int)totalMilliseconds, cancellationToken);
        }

        /// <exception cref="OperationCanceledException"/>
        [DebuggerStepThrough]
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return WaitAsync(Timeout.Infinite, cancellationToken);
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="OperationCanceledException"/>
        public Task<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (millisecondsTimeout < -1)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(millisecondsTimeout));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            lock (_awaiters)
            {
                if (_set)
                {
                    return GlobalVars.CompletedTrueTask;
                }
                else
                // Становимся в очередь на получение сигнала.
                {
                    if (millisecondsTimeout != 0)
                    {
                        var item = new QueueAwaiter(OnAwaiterCancels, millisecondsTimeout, cancellationToken);
                        _awaiters.Enqueue(item);
                        return item.Task;
                    }
                    else
                    {
                        return GlobalVars.CompletedFalseTask;
                    }
                }
            }
        }

        private void OnAwaiterCancels(QueueAwaiter awaiter)
        {
            lock (_awaiters)
            {
                // PS: в редком случае, метод Set мог обогнать и уже удалить из коллекции.
                _awaiters.Remove(awaiter);
            }
        }

        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly AsyncManualResetEventEx _mre;

            public DebugView(AsyncManualResetEventEx mre)
            {
                _mre = mre;
            }

            public bool IsSet => _mre.IsSet;

            public int WaitQueueCount => _mre._awaiters.Count;
        }
    }
}
