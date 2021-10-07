using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks.Sources;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            ThrottleExample();
            Console.ReadKey();
        }

        static void ThrottleExample()
        {
            using (var throttle = new Throttle<int>(callback: p => UiShowProgress(p), 100))
            {
                for (int i = 0; i <= 100; i++)
                {
                    throttle.Invoke(i);
                    Thread.Sleep(10);
                }
            }

            // At this point, we know that callback is completed and will no longer be caused by the Throttle.
            UiShowProgress(100);

            static void UiShowProgress(int progress)
            {
                Console.WriteLine(progress);
            }
        }
    }
}
