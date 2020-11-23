using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class AutoResetEventTest
    {
        static void Main()
        {
            var t = new PrioritizedTaskScheduler(ThreadPriority.Highest);

            Task.Factory.StartNew(async () =>
            {
                var sc = SynchronizationContext.Current;

                Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " " + Thread.CurrentThread.Priority);
                await Task.Delay(2000);
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " " + Thread.CurrentThread.Priority);

            }, default, TaskCreationOptions.None, t).Wait();


            //Task.Factory.StartNew(async () =>
            //{
            //    while (true)
            //    {
            //        Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " " + Thread.CurrentThread.Priority);
            //        await Task.Delay(-1);
            //        Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " " + Thread.CurrentThread.Priority);
            //    }

            //}, default, TaskCreationOptions.None, t)
            //    .Wait();

            Thread.Sleep(-1);
        }
    }
}
