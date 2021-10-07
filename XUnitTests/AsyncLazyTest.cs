using DanilovSoft.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace XUnitTests
{
    public class AsyncLazyTest
    {
        [Fact]
        public async Task Test1()
        {
            var lazy = new AsyncLazy<int>(async () => await Task.FromResult(123), cacheFailure: false);
            int value = await lazy.GetValueAsync();
            Assert.Equal(123, value);
        }

        [Fact]
        public async Task TestExceptionCache()
        {
            int tryes = 0;

            var lazy = new AsyncLazy<int>(async () => 
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
            var lazy = new AsyncLazy<int>(() =>
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
    }
}
