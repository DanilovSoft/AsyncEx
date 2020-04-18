using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            using var scheduller = new CustomPriorityTaskScheduller(2, ThreadPriority.Lowest);

            var actionBlock = new ActionBlock<int>(async x => 
            {
                await Task.Delay(2000);

                Console.WriteLine(x + " Thread: " + Thread.CurrentThread.Name);

            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 10, BoundedCapacity = -1, TaskScheduler = scheduller });

            actionBlock.Post(0);
            actionBlock.Post(1);
            actionBlock.Post(2);
            actionBlock.Post(3);
            actionBlock.Post(4);
            actionBlock.Post(5);
            actionBlock.Post(6);
            actionBlock.Post(7);
            actionBlock.Post(8);
            actionBlock.Post(9);
            actionBlock.Post(10);
            actionBlock.Post(11);
            actionBlock.Post(12);

            await actionBlock.Completion;
        }
    }
}
