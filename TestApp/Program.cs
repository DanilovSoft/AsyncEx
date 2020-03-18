using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Task t = Task.CompletedTask;
            t.WaitAsync(default);
        }
    }
}
