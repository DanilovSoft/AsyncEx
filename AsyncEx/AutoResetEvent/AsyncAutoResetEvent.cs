using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace DanilovSoft.AsyncEx
{
    public sealed class AsyncAutoResetEvent : INotifyCompletion
    {
        // m_continuationObject is set to this when the task completes.
        private static readonly object s_taskCompletionSentinel = new object();

        // Can be null, a single continuation, a list of continuations, or s_taskCompletionSentinel,
        // in that order. The logic arround this object assumes it will never regress to a previous state.
        private volatile object? m_continuationObject = null;

        private int _state;

        public AsyncAutoResetEvent(bool initialState)
        {
            
        }

        public ValueTask<bool> WaitOneAsync()
        {
            if (Interlocked.CompareExchange(ref _state, 0, 1) == 1)
            // Забрали сигнал.
            {
                return new ValueTask<bool>(result: true);
            }
            else
            // Сигнал занят — нужно ждать.
            {
                Task<bool> task = WaitForSignalAsync();
                return new ValueTask<bool>(task);
            }
        }

        private async Task<bool> WaitForSignalAsync()
        {
            return await this;
        }

        private void ResetSignal()
        {
            
        }

        public void Set()
        {
            // Должны разбудить один ожидающий await если он есть.
            

            
        }

        public bool Reset()
        {
            return default;
        }

        internal AsyncAutoResetEvent GetAwaiter() => this;
        internal bool IsCompleted { get; }

        void INotifyCompletion.OnCompleted(Action continuation)
        {
            // Try to just jam tc into m_continuationObject
            if ((m_continuationObject != null) || (Interlocked.CompareExchange(ref m_continuationObject, continuation, null) != null))
            {
                // If we get here, it means that we failed to CAS tc into m_continuationObject.
                // Therefore, we must go the more complicated route.
                AddTaskContinuationComplex(continuation);

                //ExecutionContext.Capture()
            }
        }

        private void AddTaskContinuationComplex(Action continuation)
        {

        }

        internal bool GetResult()
        {
            throw new NotImplementedException();
        }
    }
}
