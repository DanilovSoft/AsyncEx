using DanilovSoft.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace XUnitTests
{
    public class AsyncAutoResetEventTest
    {
        public async Task Release()
        {
            var a = new AsyncAutoResetEvent();

            _ = Task.Delay(2000).ContinueWith(_ => a.Set());

            await a.WaitAsync();
        }

        public async Task TimedOut()
        {
            var a = new AsyncAutoResetEvent();

            bool timedout = await a.WaitAsync(1000);
            Assert.False(timedout);
        }

        public async Task NotTimedOut()
        {
            var a = new AsyncAutoResetEvent();

            _ = Task.Delay(500).ContinueWith(_ => a.Set());

            bool timedout = await a.WaitAsync(1000);
            Assert.True(timedout);
        }

        public async Task DelayCanceled()
        {
            var a = new AsyncAutoResetEvent();

            using var cts = new CancellationTokenSource(1000);

            try
            {
                await a.WaitAsync(10_000, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            Assert.True(false);
        }

        public async Task DelayCanceled2()
        {
            var a = new AsyncAutoResetEvent();

            using var cts = new CancellationTokenSource(1000);

            try
            {
                await a.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            Assert.True(false);
        }
    }
}
