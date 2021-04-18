using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    internal sealed class QueueAwaiter
    {
        private readonly Queue<QueueAwaiter> _awaiters;
        private readonly TaskCompletionSource<bool> _tcs;
        private readonly CancellationTokenRegistration _canc;
        private readonly CancellationToken _cancellationToken;
        private readonly Timer? _timer;

        public QueueAwaiter(Queue<QueueAwaiter> awaiters, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            Debug.Assert(millisecondsTimeout != 0);

            _awaiters = awaiters;
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
                RemoveSelf();
            }
        }

        private void TryCancel()
        {
            if (_tcs.TrySetCanceled(_cancellationToken))
            {
                Cleanup();
                RemoveSelf();
            }
        }

        private void RemoveSelf()
        {
            lock (_awaiters)
            {
                // PS: в редком случае, метод Set мог обогнать и уже удалить из коллекции.
                _awaiters.Remove(this);
            }
        }

        private void Cleanup()
        {
            _timer?.Dispose();
            _canc.Dispose(); // можно диспозить несколько раз.
        }
    }
}
