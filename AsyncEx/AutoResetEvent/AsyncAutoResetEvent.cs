﻿using System;
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
                    _set = false;
                    return GlobalVars.CompletedTrueTask;
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

        private sealed class QueueAwaiter
        {
            private readonly AsyncAutoResetEvent _are;
            private readonly TaskCompletionSource<bool> _tcs;
            private readonly CancellationTokenRegistration _canc;
            private readonly CancellationToken _cancellationToken;
            private readonly Timer? _timer;

            public QueueAwaiter(AsyncAutoResetEvent are, int millisecondsTimeout, CancellationToken cancellationToken)
            {
                Debug.Assert(millisecondsTimeout != 0);

                _are = are;
                _cancellationToken = cancellationToken;
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (millisecondsTimeout != Timeout.Infinite)
                {
                    _timer = new Timer(static state => ((QueueAwaiter)state!).TrySetTimeout(), this, millisecondsTimeout, Timeout.Infinite);
                    if (_tcs.Task.IsCompleted) // волатильное свойство (проверил в исходнике).
                    {
                        // Поддержим редкий случай когда таймер может сработать быстрее чем мы запишем его хендлер в переменную,
                        // в этом случае таймер не будет освобождён должным образом.
                        // Этот вызов Dispose может соревноваться с вызовом Cleanup, но Dispose таймера потокобезопасен
                        // и повторные вызовы проигнорируются (проверил в исходнике).
                        _timer.Dispose();
                    }
                }

                if (cancellationToken.CanBeCanceled)
                {
                    // Может сработать сразу в текущем потоке.
                    _canc = cancellationToken.UnsafeRegister(static state => ((QueueAwaiter)state!).TryCancel(), this);
                    if (_tcs.Task.IsCompleted)
                    {
                        _canc.Dispose();
                    }
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

            private void Cleanup()
            {
                _timer?.Dispose();
                _canc.Dispose(); // можно диспозить несколько раз.
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
