using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var mre = new AsyncManualResetEvent(false);

            await mre.WaitAsync();
        }
    }
}
