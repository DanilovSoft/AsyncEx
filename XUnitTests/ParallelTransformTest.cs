namespace XUnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DanilovSoft.AsyncEx;
    using Xunit;

    public class ParallelTransformTest
    {
        [Fact]
        public async Task TestException()
        {
            var users = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            try
            {
                await ParallelTransform.Run(users, x => GetOrderProducts(x), 2);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }
            Assert.True(false);
        }

        private Task<string> GetOrderProducts(int userId)
        {
            if (userId == 1)
                throw new ArgumentOutOfRangeException(nameof(userId));

            return GetOrderProducts2(userId);
        }

        private async Task<string> GetOrderProducts2(int userId)
        {
            await Task.Delay(5_000);
            return userId.ToString();
        }
    }
}
