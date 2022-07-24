using System;
using System.Threading.Tasks;

namespace XUnitTests
{
    public class AsyncLazyTest
    {
        [Fact]
        public async Task Test1()
        {
            var lazy = new AsyncLazy<int>(async delegate { return await Task.FromResult(123); }, cacheFailure: false);
            int value = await lazy.GetValueAsync();
            Assert.Equal(123, value);
        }

        [Fact]
        public async Task TestExceptionCache()
        {
            int tryes = 0;

            var lazy = new AsyncLazy<int>(async delegate
            {
                await Task.Delay(500);

                if (tryes++ == 0)
                {
                    throw new InvalidOperationException();
                }

                return 0;
            }, 
            cacheFailure: false);

            int value;
            try
            {
                value = await lazy.GetValueAsync();
                Assert.True(false);
            }
            catch (InvalidOperationException)
            {
                
            }

            value = await lazy.GetValueAsync();

            Assert.Equal(0, value);
        }

        [Fact]
        public async Task TestSyncException()
        {
            var lazy = new AsyncLazy<int>(delegate
            {
                throw new InvalidOperationException();
                return Task.FromResult(0);
            }, 
            cacheFailure: false);


            try
            {
                await lazy.GetValueAsync();
                Assert.True(false);
            }
            catch (Exception)
            {

            }
        }

        [Fact]
        public void Thread2WaitAndGetCanceled()
        {
            var lazy = new AsyncLazy<int>(async (_, ct) =>
            {
                await Task.Delay(-1, ct);
                return 0;
            },
            null,
            cacheFailure: true);

            var task1 = lazy.GetValueAsync();
            var task2 = lazy.GetValueAsync(new CancellationToken(true));

            task2.ContinueWith(_ => { }).Wait();

            Assert.True(!task1.IsCompleted);
            Assert.True(task2.IsCanceled);
        }

        [Fact]
        public void CancelAsyncFactory_Thread2Repeats()
        {
            SemaphoreSlim sem = new(0);

            var lazy = new AsyncLazy<int>(async (_, ct) =>
            {
                await sem.WaitAsync(ct);
                return 0;
            },
            null,
            cacheFailure: true);

            var task1 = lazy.GetValueAsync(new CancellationToken(true)); // Выполнится синхронно.
            var task2 = lazy.GetValueAsync();

            sem.Release(2);
            task1.ContinueWith(_ => { }).Wait();

            Assert.True(task1.IsCanceled);
            Assert.True(task2.IsCompletedSuccessfully);
        }

        [Fact]
        public void CancelSyncFactory_Thread2Repeats()
        {
            var lazy = new AsyncLazy<int>((_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(0);
            },
            null,
            cacheFailure: true);

            try
            {
                _ = lazy.GetValueAsync(new CancellationToken(true));
                Assert.True(false);
            }
            catch (OperationCanceledException)
            {
            }

            var task2 = lazy.GetValueAsync();

            Assert.True(task2.IsCompletedSuccessfully);
        }

        [Fact]
        public void FaultSyncFactory_Thread2Faulted()
        {
            var n = 0;

            var lazy = new AsyncLazy<int>((_, ct) =>
            {
                if (n++ == 0)
                {
                    return Task.FromException<int>(new OperationCanceledException(ct));
                }
                return Task.FromResult(0);
            },
            null,
            cacheFailure: true);

            var task1 = lazy.GetValueAsync(); // Выполнится синхронно.
            var task2 = lazy.GetValueAsync();

            Assert.True(task1.IsFaulted);
            Assert.True(task2.IsFaulted);
        }

        [Fact]
        public void CancelAsyncFactory_Thread2WaitAndRepeats()
        {
            SemaphoreSlim sem = new(0);

            var lazy = new AsyncLazy<int>(async (_, ct) =>
            {
                await sem.WaitAsync(CancellationToken.None);
                ct.ThrowIfCancellationRequested();
                return 0;
            },
            null,
            cacheFailure: true);

            var task1 = lazy.GetValueAsync(new CancellationToken(true));
            var task2 = lazy.GetValueAsync();

            sem.Release(2);

            task1.ContinueWith(_ => { }).Wait();
            task2.ContinueWith(_ => { }).Wait();

            Assert.True(task1.IsCanceled);
            Assert.True(task2.IsCompletedSuccessfully);
        }
    }
}
