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
            //await MainAsync();

            var lazy = new AsyncLazy<int>(async ct => await GetValueAsync(ct));

            //var cts = new CancellationTokenSource(1000);
            //var task = Task.Run(() => lazy.GetValueAsync(cts.Token));

            //Thread.Sleep(500);


            int retryLeft = 1;
        Retry:
            var cts2 = new CancellationTokenSource(6000);
            try
            {
                var value = await lazy.GetValueAsync(cts2.Token);
            }
            catch (OperationCanceledException) when (!cts2.IsCancellationRequested && retryLeft > 0)
            // Произошла отмена явно не по нашей инициативе. Но это может быть просто исключение таймаута.
            {
                --retryLeft;
                goto Retry;
            }
        }

        static async Task<int> GetValueAsync(CancellationToken cancellationToken)
        {
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(100);
            await httpClient.GetAsync("https://ya.ru", cancellationToken);
            return 0;
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
