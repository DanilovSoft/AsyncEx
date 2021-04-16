using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static readonly Task<bool> CompletedTrueTask = Task.FromResult(true);
        private static readonly Task<bool> CompletedFalseTask = Task.FromResult(false);
        private readonly Queue<QueueAwaiter> _awaiters = new();
        private bool _set;

        public AsyncAutoResetEvent() : this(initialState: false) { }

        public AsyncAutoResetEvent(bool initialState)
        {
            _set = initialState;
        }

        public bool IsSet { get => Volatile.Read(ref _set); }

        public void Set()
        {
            // Должны или отпустить один из ожидающих Task либо установить сигнальное состояние.
            lock (_awaiters)
            {
                skip:
                if (_awaiters.TryDequeue(out var awaiter))
                {
                    if (!awaiter.TrySet())
                    {
                        // Редкий случай когда проигрываем гонку с отменой.
                        goto skip;
                    }
                }
                else
                {
                    // Больше нет потоков ожидающих сигнал.
                    _set = true;
                }
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
                throw new ArgumentOutOfRangeException(nameof(timeout));
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
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
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
                    return CompletedTrueTask;
                }
                else
                // Становимся в очередь на получение сигнала.
                {
                    if (millisecondsTimeout != 0)
                    {
                        var item = new QueueAwaiter(this, millisecondsTimeout, cancellationToken);
                        _awaiters.Enqueue(item);
                        return item.Task;
                    }
                    else
                    {
                        return CompletedFalseTask;
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

        private sealed class QueueAwaiter
        {
            private readonly TaskCompletionSource<bool> _tcs;
            private readonly CancellationTokenRegistration _canc;
            private readonly AsyncAutoResetEvent _are;
            private readonly CancellationToken _cancellationToken;
            private readonly Timer? _timer;

            public QueueAwaiter(AsyncAutoResetEvent are, int millisecondsTimeout, CancellationToken cancellationToken)
            {
                _are = are;
                _cancellationToken = cancellationToken;
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (millisecondsTimeout > 0)
                {
                    // Может сработать сразу.
                    _timer = new Timer(static state => ((QueueAwaiter)state!).TrySetTimeout(), this, millisecondsTimeout, Timeout.Infinite);
                }

                if (cancellationToken.CanBeCanceled)
                {
                    // Может сработать сразу в текущем потоке.
                    _canc = cancellationToken.UnsafeRegister(static state => ((QueueAwaiter)state!).TryCancel(), this);
                }

                if (_tcs.Task.IsCompleted)
                {
                    _canc.Dispose();
                    _timer?.Dispose();
                }
            }

            public Task<bool> Task => _tcs.Task;

            public bool TrySet()
            {
                if (_tcs.TrySetResult(true))
                {
                    Cleanup();
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private void Cleanup()
            {
                _timer?.Dispose();
                _canc.Dispose(); // можно диспозить несколько раз.
            }

            private void TrySetTimeout()
            {
                if (_tcs.TrySetResult(false))
                {
                    Cleanup();
                    _are.OnAwaiterCancels(this);
                }
            }

            private void TryCancel()
            {
                if (_tcs.TrySetCanceled(_cancellationToken))
                {
                    Cleanup();
                    _are.OnAwaiterCancels(this);
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
