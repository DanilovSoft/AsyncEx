using DanilovSoft.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace XUnitTests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var ctts = new CancellationTokenTaskSource(cts.Token);

            try
            {
                await ctts.Task;
            }
            catch (OperationCanceledException ex)
            {
                Assert.Equal(cts.Token, ex.CancellationToken);
            }
        }

        [Fact]
        public async Task TestCancellableTask()
        {
            var cts = new CancellationTokenSource();
            var task = DanilovSoft.Threading.Tasks.TaskExtensions.WaitAsync(Task.Delay(-1), cts.Token);
            cts.Cancel();

            try
            {
                await task;
            }
            catch (OperationCanceledException ex)
            {
                Assert.Equal(cts.Token, ex.CancellationToken);
            }
        }

        [Fact]
        public async Task TestMre()
        {
            var mre = new AsyncManualResetEvent(false);

            _ = Task.Delay(2000).ContinueWith(_ => { mre.Set(); });

            await mre.WaitAsync();
            await mre.WaitAsync();

            mre.Reset();
        }

        [Fact]
        public async Task TestMreCancel()
        {
            var mre = new AsyncManualResetEvent(false);

            var cts = new CancellationTokenSource(2000);
            //_ = Task.Delay(2000).ContinueWith(_ => { mre.Set(); });

            ValueTask task = mre.WaitAsync(cts.Token);

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                Assert.True(task.IsCanceled);
            }
            

            mre.Reset();
        }
    }
}
