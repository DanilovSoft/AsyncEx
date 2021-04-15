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
            var mre = new ManualResetEventSlim(initialState: true);
            mre.Wait(default(TimeSpan));

            //var mre = new ManualResetValueTaskSourceCore<int>();
            //mre.Version
        }
    }
}
