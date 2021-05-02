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
            var lazy = new AsyncLazy<int>(() => 
            {
                Thread.Sleep(1000);
                throw new InvalidOperationException();
                return Task.FromResult(1);
            }, 
            true);

            for (int i = 0; i < 10; i++)
            {
                _ = Task.Run(() =>
                {
                    _ = lazy.Task;
                });
            }

            Thread.Sleep(-1);
        }
    }
}
