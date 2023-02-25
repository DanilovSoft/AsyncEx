using System.Threading.Tasks;

namespace XUnitTests;

public class ThrottleTests
{
    [Fact]
    public void Throttle_Once_Success()
    {
        var controlValue = -1;

        using (var throttle = new Throttle(s => controlValue = (int)s))
        {
            throttle.Invoke(200, 1);
            Thread.Sleep(100);

            Assert.Equal(-1, controlValue);

            throttle.Invoke(200, 2);
            Thread.Sleep(150);

            Assert.Equal(2, controlValue);
        }
    }

    [Fact]
    public void Throttle_Twice_Success()
    {
        var controlValue = -1;

        using (var throttle = new Throttle(s => controlValue = (int)s))
        {
            throttle.Invoke(200, 1);
            Thread.Sleep(100);

            Assert.Equal(-1, controlValue);

            throttle.Invoke(200, 2);
            Thread.Sleep(150);

            Assert.Equal(2, controlValue);

            throttle.Invoke(200, 3);
            Thread.Sleep(300);

            Assert.Equal(3, controlValue);
        }
    }

    [Fact]
    public void Cancel_Success()
    {
        var controlValue = -1;

        using (var throttle = new Throttle(s => controlValue = (int)s))
        {
            throttle.Invoke(200, 1);
            Thread.Sleep(100);
        }

        Thread.Sleep(200);

        Assert.Equal(-1, controlValue);
    }

    [Fact]
    public void WaitDispose()
    {
        var controlValue = -1;

        using (var throttle = new Throttle(s => { Thread.Sleep(2000); controlValue = (int)s; }))
        {
            throttle.Invoke(100, 1);
            Thread.Sleep(200);
        }

        Assert.Equal(1, controlValue);
    }

    [Fact]
    public async Task WaitDisposeAsync()
    {
        var controlValue = -1;

        await using (var throttle = new Throttle(s => { Thread.Sleep(2000); controlValue = (int)s; }))
        {
            throttle.Invoke(100, 1);
            Thread.Sleep(200);
        }

        Assert.Equal(1, controlValue);
    }
}
