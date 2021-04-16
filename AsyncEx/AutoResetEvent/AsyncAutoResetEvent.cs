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
    public sealed class AsyncAutoResetEvent
    {
        private static readonly Task<bool> CompletedTrueTask = Task.FromResult(true);
        private static readonly Task<bool> CompletedFalseTask = Task.FromResult(false);
        private readonly Queue<QueueAwaiter> _awaiters = new();
        private bool _signaled;

        public AsyncAutoResetEvent() : this(initialState: false) { }

        public AsyncAutoResetEvent(bool initialState)
        {
            _signaled = initialState;
        }

        public bool IsSet { get => throw new NotImplementedException(); }

        public void Set()
        {
            lock (_awaiters)
            {
                if (_awaiters.TryDequeue(out var awaiter))
                {
                    awaiter.TrySet();
                }
                else
                {
                    _signaled = true;
                }
            }
        }

        public void Reset()
        {

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
                if (_signaled)
                {
                    _signaled = false;
                    return CompletedTrueTask;
                }
                else
                // Становимся в очередь на получение сигнала.
                {
                    if (millisecondsTimeout > 0)
                    {
                        var item = new QueueAwaiter(millisecondsTimeout, cancellationToken);
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

        private sealed class QueueAwaiter
        {
            private readonly TaskCompletionSource<bool> _tcs;
            private readonly CancellationTokenRegistration _canc;
            private readonly CancellationToken _cancellationToken;
            private Timer _timer;

            public QueueAwaiter(int millisecondsTimeout, CancellationToken cancellationToken)
            {
                Debug.Assert(millisecondsTimeout > 0);

                _cancellationToken = cancellationToken;
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                
                // Может сработать сразу.
                var timer = new Timer(static state => ((QueueAwaiter)state!).TrySetTimeout(), this, Timeout.Infinite, Timeout.Infinite);
                _timer = timer;

                // Может сработать сразу.
                _canc = cancellationToken.Register(static state => ((QueueAwaiter)state!).TryCancel(), this, useSynchronizationContext: false);
                if (_tcs.Task.IsCompleted)
                {
                    _canc.Dispose();
                    timer.Dispose();
                }
            }

            public Task<bool> Task => _tcs.Task;

            public void TrySet()
            {
                if (_tcs.TrySetResult(true))
                {
                    Cleanup();
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
                }
            }

            private void TryCancel()
            {
                if (_tcs.TrySetCanceled(_cancellationToken))
                {
                    Cleanup();
                }
            }
        }
    }
}
