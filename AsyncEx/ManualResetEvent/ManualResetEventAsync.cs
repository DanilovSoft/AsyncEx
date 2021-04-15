using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public sealed class ManualResetEventAsync
    {
        public ManualResetEventAsync() : this(initialState: false)
        {

        }

        public ManualResetEventAsync(bool initialState)
        {

        }

        public bool IsSet { get => throw new NotImplementedException(); }

        public void Set()
        {

        }

        public void Reset()
        {

        }

        public ValueTask WaitAsync()
        {
            return WaitAsync(CancellationToken.None);
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        public ValueTask<bool> WaitAsync(int millisecondsTimeout)
        {
            return WaitAsync(millisecondsTimeout, CancellationToken.None);
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        public ValueTask<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitAsync(timeout, CancellationToken.None);
        }
        
        /// <exception cref="OperationCanceledException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            return WaitAsync((int)totalMilliseconds, cancellationToken);
        }

        /// <exception cref="OperationCanceledException"/>
        public ValueTask WaitAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="OperationCanceledException"/>
        public ValueTask<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
            }
            throw new NotImplementedException();
        }
    }
}
