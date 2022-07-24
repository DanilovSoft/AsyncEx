using System.Threading.Tasks;

namespace XUnitTests
{
    public class AsyncLockTest
    {
        [Fact]
        public async Task LockTest()
        {
            var locker = new AsyncLock();
            
            using (await locker.LockAsync())
            {

            }
        }
    }
}
