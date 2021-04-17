using DanilovSoft.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace XUnitTests
{
    public class LazyAsyncTest
    {
        [Fact]
        public async Task Test1()
        {
            var lazy = new LazyAsync<int>(PauseAsync);
            int value = await lazy.GetValueAsync();
            Assert.Equal(123, value);
        }

        private async Task<int> PauseAsync()
        {
            await Task.Delay(2000);
            return 123;
        }
    }
}
