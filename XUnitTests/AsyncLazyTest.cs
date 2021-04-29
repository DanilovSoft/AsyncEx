using DanilovSoft.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace XUnitTests
{
    public class AsyncLazyTest
    {
        [Fact]
        public async Task Test1()
        {
            static async Task<int> CreateValueAsync()
            {
                await Task.Delay(2000);
                return 123;
            }

            var lazy = new AsyncLazy<int>(CreateValueAsync);
            int value = await lazy.GetValueAsync();
            Assert.Equal(123, value);
        }

        [Fact]
        public async Task TestExceptionCache()
        {
            int tryes = 0;

            var lazy = new AsyncLazy<int>(async () => 
            {
                await Task.Delay(1000);

                if (tryes++ == 0)
                    throw new InvalidOperationException();

                return 0;
            });

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
                throw new NotSupportedException();
                return Task.FromResult(0);
            }, 
            retryOnFailure: true);


            try
            {
                await lazy.GetValueAsync();
            }
            catch (Exception)
            {
                Assert.True(false);
            }
        }
    }
}
