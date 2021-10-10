using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace DanilovSoft.AsyncEx
{
    [DebuggerDisplay("IsSet = {IsSet}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class AsyncAutoResetEvent
    {
        private readonly Queue<QueueAwaiter> _awaiters = new();
        private bool _set;

        public AsyncAutoResetEvent() : this(initialState: false) { }

        public AsyncAutoResetEvent(bool initialState)
        {
            _set = initialState;
        }

        public bool IsSet => Volatile.Read(ref _set);

        public void Set()
        {
            // Должны или отпустить один из ожидающих Task либо установить сигнальное состояние.
            lock (_awaiters)
            {
                Skip:
                if (_awaiters.TryDequeue(out var awaiter))
                {
                    if (!awaiter.TrySet())
                    {
                        // Редкий случай когда проигрываем гонку с отменой.
                        goto Skip;
                    }
                }
                else
                {
                    // Больше нет потоков ожидающих сигнал.
                    _set = true;
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
            return WaitAsync(CancellationToken.None);
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
                ThrowHelper.ThrowArgumentOutOfRange(nameof(timeout));
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
                ThrowHelper.ThrowArgumentOutOfRange(nameof(millisecondsTimeout));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            lock (_awaiters)
            {
                if (_set)
                {
                    _set = false;
                    return GlobalVars.CompletedTrueTask;
                }
                else
                // Становимся в очередь на получение сигнала.
                {
                    if (millisecondsTimeout != 0)
                    {
                        var item = new QueueAwaiter(_awaiters, millisecondsTimeout, cancellationToken);
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

        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly AsyncAutoResetEvent _are;

            public DebugView(AsyncAutoResetEvent are)
            {
                _are = are;
            }

            public bool IsSet => _are.IsSet;

            public int WaitQueueCount => _are._awaiters.Count;
        }
    }
}
