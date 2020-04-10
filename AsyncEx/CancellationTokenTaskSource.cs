using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.Threading
{
    internal sealed class CancellationTokenTaskSource : IDisposable
#if !NETSTANDARD2_0
        , IAsyncDisposable
#endif
    {
        private readonly TaskCompletionSource<VoidStruct> _tcs;
        private readonly CancellationTokenRegistration _reg;
        private readonly CancellationToken _cancellationToken;
        public Task Task => _tcs.Task;

        public CancellationTokenTaskSource(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _tcs = new TaskCompletionSource<VoidStruct>(TaskCreationOptions.RunContinuationsAsynchronously);

#if NETSTANDARD2_0

            _reg = cancellationToken.Register(OnCanceled, this, useSynchronizationContext: false);
#else
            // ExecutionContext не захватывается и не передаётся в колбеки.
            _reg = cancellationToken.UnsafeRegister(OnCanceled, this);
#endif
        }

        private static void OnCanceled(object? state)
        {
            var self = state as CancellationTokenTaskSource;
            Debug.Assert(self != null);
            self._tcs.TrySetCanceled(self._cancellationToken);
        }

#if !NETSTANDARD2_0

        public ValueTask DisposeAsync()
        {
            return _reg.DisposeAsync();
        }
#endif
        public void Dispose()
        {
            _reg.Dispose();
        }
    }
}
