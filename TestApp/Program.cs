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
            var lazy1 = new AsyncLazy<int>(async () => await GetValue());
            var lazy2 = new AsyncLazy<int>(async ct => await GetValue(ct));
            var lazy3 = new AsyncLazy<int>("state", async s => await GetValue());
            var lazy4 = new AsyncLazy<int>("state", async (s, ct) => await GetValue(ct));
            var lazy5 = new AsyncLazy<int>("state", async (s, ct) => await GetValue(ct), cacheFailure: false);
        }

        static async Task<int> GetValue(CancellationToken cancellationToken = default)
        {
            return 1;
        }
    }
}
