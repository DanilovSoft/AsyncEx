using System.Threading;
using System.Threading.Tasks;

namespace XUnitTests
{
    public class ThrottleTests
    {
        [Fact]
        public void Throttle_Once_Success()
        {
            int controlValue = -1;

            using (var throttle = new Throttle<int>(v => controlValue = v, 200))
            {
                throttle.Invoke(1);
                Thread.Sleep(100);

                Assert.Equal(-1, controlValue);

                throttle.Invoke(2);
                Thread.Sleep(150);

                Assert.Equal(2, controlValue);
            }
        }

        [Fact]
        public void Throttle_Twice_Success()
        {
            int controlValue = -1;

            using (var throttle = new Throttle<int>(v => controlValue = v, 200))
            {
                throttle.Invoke(1);
                Thread.Sleep(100);

                Assert.Equal(-1, controlValue);

                throttle.Invoke(2);
                Thread.Sleep(150);

                Assert.Equal(2, controlValue);

                throttle.Invoke(3);
                Thread.Sleep(300);

                Assert.Equal(3, controlValue);
            }
        }

        [Fact]
        public void Cancel_Success()
        {
            int controlValue = -1;

            using (var throttle = new Throttle<int>(v => controlValue = v, 200))
            {
                throttle.Invoke(1);
                Thread.Sleep(100);
            }
            Thread.Sleep(200);

            Assert.Equal(-1, controlValue);
        }

        [Fact]
        public void WaitDispose()
        {
            int controlValue = -1;

            using (var throttle = new Throttle<int>(v => { Thread.Sleep(2000); controlValue = v; }, 100))
            {
                throttle.Invoke(1);
                Thread.Sleep(200);
            }

            Assert.Equal(1, controlValue);
        }

        [Fact]
        public async Task WaitDisposeAsync()
        {
            int controlValue = -1;

            await using (var throttle = new Throttle<int>(v => { Thread.Sleep(2000); controlValue = v; }, 100))
            {
                throttle.Invoke(1);
                Thread.Sleep(200);
            }

            Assert.Equal(1, controlValue);
        }
    }
}
