using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public sealed class CompletionCountdownEvent
    {
        public Task Completion { get; }

        public bool TryIncrement()
        {
            throw new NotImplementedException();
        }

        public void TryDecrement()
        {
            throw new NotImplementedException();
        }

        public void Complete()
        {
            throw new NotImplementedException();
        }

        public void TryCancel()
        {
            throw new NotImplementedException();
        }

        public void TryFault(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void TryCancel(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
