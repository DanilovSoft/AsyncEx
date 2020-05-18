using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    // Provides a task scheduler that ensures a maximum concurrency level while 
    // running on top of the thread pool.
    public sealed class PrioritizedTaskScheduler : TaskScheduler
    {
        // The list of tasks to be executed 
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

        private readonly ThreadPriority _threadsPriority;

        //// Indicates whether the current thread is processing work items.
        //[ThreadStatic]
        //private static bool _currentThreadIsProcessingItems;

        // Indicates whether the scheduler is currently processing work items. 
        //private int _delegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism. 
        public PrioritizedTaskScheduler(ThreadPriority threadsPriority)
        {
            if (!Enum.IsDefined(typeof(ThreadPriority), _threadsPriority))
                throw new ArgumentOutOfRangeException(nameof(threadsPriority));

            _threadsPriority = threadsPriority;
        }

        // Queues a task to the scheduler. 
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks)
            {
                _tasks.AddLast(task);
                //if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    //++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
#if NETCOREAPP3_1
            // TODO проверить уместность preferLocal.
            ThreadPool.UnsafeQueueUserWorkItem(ThreadStaticEntry, this, preferLocal: true);
#else
            ThreadPool.UnsafeQueueUserWorkItem(ThreadStaticEntry, this);
#endif
        }

        private static void ThreadStaticEntry(object? state)
        {
            var self = state as PrioritizedTaskScheduler;
            Debug.Assert(self != null);
            self.ThreadEntry();
        }

        private void ThreadEntry()
        {
            var prio = Thread.CurrentThread.Priority;
            if (prio != _threadsPriority)
            {
                Thread.CurrentThread.Priority = _threadsPriority;
            }

            // Note that the current thread is now processing work items.
            // This is necessary to enable inlining of tasks into this thread.
            //_currentThreadIsProcessingItems = true;
            try
            {
                // Process all available items in the queue.
                while (true)
                {
                    Task item;
                    lock (_tasks)
                    {
                        // When there are no more items to be processed,
                        // note that we're done processing, and get out.
                        if (_tasks.Count == 0)
                        {
                            //--_delegatesQueuedOrRunning;
                            break;
                        }

                        // Get the next item from the queue
                        item = _tasks.First!.Value;
                        _tasks.RemoveFirst();
                    }

                    // Execute the task we pulled out of the queue
                    TryExecuteTask(item);
                }
            }
            // We're done processing items on the current thread
            finally
            {
                if (prio != _threadsPriority)
                {
                    Thread.CurrentThread.Priority = prio;
                }
                //_currentThreadIsProcessingItems = false; 
            }
        }

        // Attempts to execute the specified task on the current thread. 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            //if (!_currentThreadIsProcessingItems) 
            //    return false;

            // Поддерживаем инлайнинг если приоритет потока нам подходит.
            if (Thread.CurrentThread.Priority != _threadsPriority)
                return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
            {
                // Try to run the task. 
                if (TryDequeue(task))
                {
                    return TryExecuteTask(task);
                }
                else
                    return false;
            }
            else
            {
                return TryExecuteTask(task);
            }
        }

        // Attempt to remove a previously scheduled task from the scheduler. 
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks)
            {
                return _tasks.Remove(task);
            }
        }

        // Gets the maximum concurrency level supported by this scheduler. 
        public sealed override int MaximumConcurrencyLevel => int.MaxValue;

        // Gets an enumerable of the tasks currently scheduled on this scheduler. 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) 
                    return _tasks;
                else 
                    throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) 
                    Monitor.Exit(_tasks);
            }
        }
    }
}
