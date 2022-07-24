using System;
using System.Threading.Tasks;

namespace XUnitTests
{
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

        [Fact]
        public async Task TestParallelTransform_Success()
        {
            var userIds = new int[] { 123, 456 };

            // Мутация дерева для обогащения свойств товаров значениями.
            var users = await ParallelTransform.Run(userIds, async x =>
            {
                return await GetUserName(x);
            },
            (id, n) => new User(id, n), maxDegreeOfParallelism: 10);

            static async Task<string> GetUserName(int userId)
            {
                await Task.Delay(100);
                return "UserName_" + userId;
            }
        }

        private Task<string> GetOrderProducts(int userId)
        {
            if (userId == 1)
            {
                throw new ArgumentOutOfRangeException(nameof(userId));
            }

            return GetOrderProducts2(userId);
        }

        private async Task<string> GetOrderProducts2(int userId)
        {
            await Task.Delay(5_000);
            return userId.ToString();
        }

        private record User(int UserId, string Name);
    }
}
