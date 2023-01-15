using System.Threading;
using System.Threading.Tasks;

namespace XUnitTests
{
    public class DebounceTests
    {
        [Fact]
        public void Debounce_Once_Success()
        {
            var controlValue = -1;

            using (var debounce = new Debounce<int>(v => controlValue = v, 200))
            {
                debounce.Invoke(1);
                Thread.Sleep(100);

                Assert.Equal(-1, controlValue);

                debounce.Invoke(2);
                Thread.Sleep(150);

                Assert.Equal(-1, controlValue);
            }
        }

        [Fact]
        public void Debounce_Twice_Success()
        {
            var controlValue = -1;

            using (var debounce = new Debounce<int>(v => controlValue = v, 200))
            {
                debounce.Invoke(1);
                Thread.Sleep(100);

                Assert.Equal(-1, controlValue);

                debounce.Invoke(2);
                Thread.Sleep(150);

                Assert.Equal(-1, controlValue);

                debounce.Invoke(3);
                Thread.Sleep(300);

                Assert.Equal(3, controlValue);
            }
        }

        [Fact]
        public void Cancel_Success()
        {
            var controlValue = -1;

            using (var debounce = new Debounce<int>(v => controlValue = v, 200))
            {
                debounce.Invoke(1);
                Thread.Sleep(100);
            }
            Thread.Sleep(300);

            Assert.Equal(-1, controlValue);
        }

        [Fact]
        public void WaitDispose()
        {
            var controlValue = -1;

            using (var debounce = new Debounce<int>(v => { Thread.Sleep(2000); controlValue = v; }, 100))
            {
                debounce.Invoke(1);
                Thread.Sleep(200);
            }

            Assert.Equal(1, controlValue);
        }

        [Fact]
        public async Task WaitDisposeAsync()
        {
            var controlValue = -1;

            await using (var debounce = new Debounce<int>(v => { Thread.Sleep(2000); controlValue = v; }, 100))
            {
                debounce.Invoke(1);
                Thread.Sleep(200);
            }

            Assert.Equal(1, controlValue);
        }
    }
}
