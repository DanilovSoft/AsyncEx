using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    [DebuggerDisplay(@"\{MaximumConcurrencyLevel = {MaximumConcurrencyLevel}\}")]
    public sealed class CustomPriorityTaskScheduller : TaskScheduler, IDisposable
    {
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>();
        private object SyncObj => _tasks;
        private readonly int _threadsCount;
        // Максимальное число потоков поддерживаемое текущим планировщиком.
        public override int MaximumConcurrencyLevel => _threadsCount;

        // Означает что текущий поток в данный момент выполняет задачи.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingTask;
        private bool _stopping;

        public CustomPriorityTaskScheduller(int threadsCount, ThreadPriority threadsPriority)
        {
            if (threadsCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(threadsCount));

            if (!Enum.IsDefined(typeof(ThreadPriority), threadsPriority))
                throw new ArgumentOutOfRangeException(nameof(threadsPriority));

            _threadsCount = threadsCount;

            for (int i = 0; i < threadsCount; i++)
            {
                var t = new Thread(ThreadEntryPoint);
                t.IsBackground = true;
                t.Priority = threadsPriority;

#if DEBUG
                t.Name = "MyThread #" + i;
#endif

                t.Start();
            }
        }

        // MTAThread
        private void ThreadEntryPoint()
        {
            while (true) // Компилится в безусловный goto.
            {
                Task? task = null;
                lock (SyncObj)
                {
                    while (_tasks.Count == 0)
                    // Спим пока нет задач.
                    {
                        if (!_stopping)
                        {
                            Monitor.Wait(SyncObj); // Wait использует барьер памяти (здесь это важно).
                        }
                        else
                            return; // Задач больше нет и был сигнал остановки — завершаем поток.
                    }

                    Debug.Assert(_tasks.First != null, "Нарушение логики работы планировщика");

                    task = _tasks.First.Value;
                    _tasks.RemoveFirst();
                }

                _currentThreadIsProcessingTask = true;
                try
                {
                    base.TryExecuteTask(task);
                }
                finally
                {
                    _currentThreadIsProcessingTask = false;
                }
            }
        }

        // Планирует задачу на выполнение в текущем планировщике. 
        protected override void QueueTask(Task task)
        {
            lock (SyncObj)
            {
                if (!_stopping)
                {
                    _tasks.AddLast(task);

                    // Разбудим один поток.
                    Monitor.Pulse(SyncObj);
                }
                else
                    throw new ObjectDisposedException(GetType().FullName);
            }
        }

        // Попытка выполнить задачу текущим потоком что-бы избежать планирования.
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // Не разрешать выполнять задачу на потоках кроме наших.
            if (!_currentThreadIsProcessingTask)
            {
                return false;
            }
            else
            {
                // Если задача была запланирована ранее то удалить из очереди.
                if (taskWasPreviouslyQueued)
                {
                    if (TryDequeue(task))
                    {
                        return base.TryExecuteTask(task);
                    }
                    else
                        return false;
                }
                else
                {
                    return base.TryExecuteTask(task);
                }
            }
        }

        // Попытка удалить ранее запланированную задачу из планировщика.
        protected override bool TryDequeue(Task task)
        {
            lock (SyncObj)
            {
                return _tasks.Remove(task);
            }
        }

        // For debugger support only, gets an enumerable of the tasks currently waiting to be executed on this scheduler.
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // When this method is called, all other threads in the process will be frozen.
            // Поэтому мы не можем синхронизироваться с другими потоками и в этом случае ДОЛЖНЫ бросить исключение.

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(SyncObj, ref lockTaken);
                if (lockTaken)
                {
                    return _tasks.ToArray(); // Вернуть копию.
                }
                else 
                    throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(SyncObj);
            }
        }

        // Безопасно останавливает запущенные потоки.
        public void Dispose()
        {
            lock (SyncObj)
            {
                // Сообщить потокам что задач больше не будет.
                _stopping = true;

                // Разбудить все потоки которые сейчас ждут задачи. 
                // PS: потоки проснутся по очереди захватывая блокировку.
                Monitor.PulseAll(SyncObj);
            }
        }
    }

    // Provides a task scheduler that ensures a maximum concurrency level while 
    // running on top of the thread pool.
    internal class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        // The list of tasks to be executed 
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

        // The maximum concurrency level allowed by this scheduler. 
        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items. 
        private int _delegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism. 
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        // Queues a task to the scheduler. 
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
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
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        // Attempts to execute the specified task on the current thread. 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) 
                return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
            {
                // Try to run the task. 
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            }
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler. 
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler. 
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler. 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }
    }
}
