using DanilovSoft;
using DanilovSoft.AsyncEx;
using DanilovSoft.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Threading
{
    [DebuggerDisplay(@"\{IsSet = {IsSet}\}")]
    public sealed class AsyncManualResetEvent
    {
        public bool IsSet => _tcs.Task.IsCompleted;
        private volatile TaskCompletionSource<VoidStruct> _tcs;

        public AsyncManualResetEvent(bool isSet)
        {
            _tcs = new TaskCompletionSource<VoidStruct>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (isSet)
                _tcs.TrySetResult(default);
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask WaitAsync() => WaitAsync(default);

        public ValueTask WaitAsync(CancellationToken cancellationToken)
        {
            // Копия volatile.
            var tcs = _tcs;

            if (tcs.Task.IsCompleted)
            {
                return new ValueTask();
            }
            else
            {
                return new ValueTask(tcs.Task.WaitAsync(cancellationToken));
            }
        }
    }
}
