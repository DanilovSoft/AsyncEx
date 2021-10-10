using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            var lazy = new AsyncLazy<int>(() => GetValueAsync(), cacheFailure: false);

            lazy.Start();

            Thread.Sleep(2000);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Thread.Sleep(-1);

            //try
            //{
            //    await lazy.GetValueAsync();
            //}
            //catch (Exception ex)
            //{

            //}
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            
        }

        static async Task<int> GetValueAsync()
        {
            throw new InvalidOperationException();
        }

        static async Task<int> GetValueAsync2(CancellationToken cancellationToken)
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMilliseconds(100);
                await httpClient.GetAsync("https://ya.ru", cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken != cancellationToken)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(200);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                    }
                }
                throw;
            }
            return 0;
        }

        static async Task MainAsync()
        {
            var cts = new CancellationTokenSource(200);
            await GetValueAsync2(cts.Token);
        }
    }

    public class LazyCanceledException : OperationCanceledException
    {
        public LazyCanceledException(CancellationToken token) : base(token)
        {
                
        }
    }
}
