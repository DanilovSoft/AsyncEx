using DanilovSoft.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.Threading.Tasks
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Asynchronously waits for the task to complete, or for the cancellation token to be canceled.
        /// </summary>
        /// <param name="task">The task to wait for. May not be <c>null</c>.</param>
        /// <param name="cancellationToken">The cancellation token that cancels the wait.</param>
        public static Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    return DoWaitAsync(task, cancellationToken);
                }
                else
                {
                    return Task.FromCanceled(cancellationToken);
                }
            }
            else
            {
                return task;
            }
        }

#if NETSTANDARD2_0
        private static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            using (var cancelTaskSource = new CancellationTokenTaskSource(cancellationToken))
            {
                await Task.WhenAny(task, cancelTaskSource.Task).Unwrap().ConfigureAwait(false);
            }
        }
#else
        private static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            var cancelTaskSource = new CancellationTokenTaskSource(cancellationToken);
            try
            {
                await Task.WhenAny(task, cancelTaskSource.Task).Unwrap().ConfigureAwait(false);
            }
            finally
            {
                await cancelTaskSource.DisposeAsync().ConfigureAwait(false);
            }
        }
#endif
    }
}
