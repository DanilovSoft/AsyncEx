using DanilovSoft.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    [DebuggerDisplay(@"\{IsSet = {IsSet}\}")]
    public sealed class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<VoidStruct> _tcs;

        public AsyncManualResetEvent(bool isSet)
        {
            _tcs = new TaskCompletionSource<VoidStruct>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (isSet)
            {
                _tcs.TrySetResult(default);
            }
        }

        public bool IsSet => _tcs.Task.IsCompleted;

        public void Set()
        {
            _tcs.TrySetResult(default);
        }

        public void Reset()
        {
            // Копия volatile.
            var tcs = _tcs;

            if (tcs.Task.IsCompleted)
            {
                var nextTcs = new TaskCompletionSource<VoidStruct>(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.CompareExchange(ref _tcs, nextTcs, tcs);
            }
        }

        [DebuggerStepThrough]
        public Task WaitAsync()
        {
            return WaitAsync(CancellationToken.None);
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            // Копия volatile.
            var tcs = _tcs;

            // TODO можно убрать 'async'.
            return tcs.Task.WaitAsync(cancellationToken);
        }
    }
}
