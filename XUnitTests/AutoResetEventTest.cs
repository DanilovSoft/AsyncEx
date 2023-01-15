using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace XUnitTests
{
    public class AutoResetEventTest
    {
        [Fact]
        public async Task Release()
        {
            var a = new AsyncAutoResetEvent();

            _ = Task.Delay(2000).ContinueWith(_ => a.Set());

            var sw = Stopwatch.StartNew();
            await a.WaitAsync();
            sw.Stop();
            Assert.InRange(sw.ElapsedMilliseconds, 2000, 10_000);
        }

        [Fact]
        public async Task TimedOut()
        {
            var a = new AsyncAutoResetEvent();

            var sw = Stopwatch.StartNew();
            var success = await a.WaitAsync(1000);
            sw.Stop();
            Assert.InRange(sw.ElapsedMilliseconds, 1000, 10_000);
            Assert.False(success);
        }

        [Fact]
        public async Task NotTimedOut()
        {
            var a = new AsyncAutoResetEvent();

            _ = Task.Delay(500).ContinueWith(_ => a.Set());

            var sw = Stopwatch.StartNew();
            var success = await a.WaitAsync(1000);
            sw.Stop();
            Assert.InRange(sw.ElapsedMilliseconds, 500, 2000);
            Assert.True(success);
        }

        [Fact]
        public async Task DelayedCancel()
        {
            var a = new AsyncAutoResetEvent();

            using var cts = new CancellationTokenSource(1000);

            var sw = Stopwatch.StartNew();
            try
            {
                await a.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Assert.InRange(sw.ElapsedMilliseconds, 1000, 10_000);
                return;
            }
            Assert.True(false);
        }

        [Fact]
        public async Task DelayedCancelWithTimeout()
        {
            var a = new AsyncAutoResetEvent();

            using var cts = new CancellationTokenSource(1000);

            var sw = Stopwatch.StartNew();
            try
            {
                await a.WaitAsync(10_000, cts.Token);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Assert.InRange(sw.ElapsedMilliseconds, 1000, 10_000);
                return;
            }
            Assert.True(false);
        }

        [Fact]
        public async Task DelayedCancelTimedOut()
        {
            var a = new AsyncAutoResetEvent();

            using var cts = new CancellationTokenSource(1000);

            var sw = Stopwatch.StartNew();
            var success = await a.WaitAsync(500, cts.Token);
            sw.Stop();
            Assert.InRange(sw.ElapsedMilliseconds, 400, 2000);
            Assert.False(success);
        }
    }
}
