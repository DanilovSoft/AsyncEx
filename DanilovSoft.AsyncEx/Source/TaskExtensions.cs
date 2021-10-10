using DanilovSoft.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Asynchronously waits for the task to complete, or for the cancellation token to be canceled.
        /// </summary>
        /// <param name="task">The task to wait for.</param>
        /// <param name="cancellationToken">The cancellation token that cancels the wait.</param>
        /// <exception cref="OperationCanceledException"/>
        public static Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (cancellationToken.CanBeCanceled && !task.IsCompleted)
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

        /// <exception cref="OperationCanceledException"/>
        private static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            if (await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false) == task)
            {
                return;
            }
            else
            // Завершился Delay из-за токена отмены.
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCompletedSuccessfully(this Task task)
        {
#if NETSTANDARD2_0
            return task.Status == TaskStatus.RanToCompletion;
#else
            return task.IsCompletedSuccessfully;
#endif
        }

        internal static void ObserveException(this Task task)
        {
            if (!task.IsCompletedSuccessfully())
            {
                task.ContinueWith(t => { _ = t.Exception; },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, 
                    TaskScheduler.Default);
            }
        }
    }
}
