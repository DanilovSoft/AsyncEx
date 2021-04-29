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
            var v = new AsyncLazy<int>(async () => 
            {
                await Task.Delay(10_000);
                return 1;
            });

            var lazy = new Lazy<int>(LazyThreadSafetyMode.ExecutionAndPublication);

            while (true)
            {
                try
                {
                    var n = await v.GetValueAsync();
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
