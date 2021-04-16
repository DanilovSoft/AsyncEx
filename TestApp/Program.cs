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
            //var a = new ManualResetEventSlim(true);

            //a.Wait(new CancellationToken(true));

            var are = new AsyncAutoResetEvent(initialState: false);

            //_ = Task.Delay(3000).ContinueWith(_ => are.Set());

            var cts = new Can
            await are.WaitAsync(5000);
            
            
            //var mre = new ManualResetValueTaskSourceCore<int>();
            //mre.Version
        }
    }
}
