using System;
using System.Threading;
using System.Threading.Tasks;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            var lazy = new LazyAsync<string?>(async () => { await Task.Delay(2000); return null; });
            lazy.Start();

            Thread.Sleep(3000);

            lazy.Value?.Trim();

            var v = await lazy.GetValueAsync();
        }
    }
}
