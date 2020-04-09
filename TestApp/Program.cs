using System;
using System.Threading;
using System.Threading.Tasks;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var lazy = new LazyAsync<int>(async () => { await Task.Delay(2000); throw new OperationCanceledException(); });
            lazy.Start();

            var v = lazy.Value;
        }
    }
}
