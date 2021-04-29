using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks.Sources;
using Nito.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            var lazy = new AsyncLazy<int>(() => 
            {
                throw new NotSupportedException();
                return Task.FromResult(0);

            } , AsyncLazyFlags.ExecuteOnCallingThread | AsyncLazyFlags.RetryOnFailure);

            try
            {
                var t = lazy.Task;
            }
            catch (Exception)
            {

            }

            try
            {
                await lazy.Task;
            }
            catch (Exception)
            {

            }
        }
    }
}
