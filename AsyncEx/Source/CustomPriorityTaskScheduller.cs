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
        private readonly LinkedList<Task> _tasks = new();
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
            {
                throw new ArgumentOutOfRangeException(nameof(threadsCount));
            }

            if (!Enum.IsDefined(typeof(ThreadPriority), threadsPriority))
            {
                throw new ArgumentOutOfRangeException(nameof(threadsPriority));
            }

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
                        {
                            return; // Задач больше нет и был сигнал остановки — завершаем поток.
                        }
                    }

                    Debug.Assert(_tasks.First != null, "Нарушение логики работы планировщика");

                    task = _tasks.First!.Value;
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
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
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
                    {
                        return false;
                    }
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
                {
                    throw new NotSupportedException();
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(SyncObj);
                }
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
}
