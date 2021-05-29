using DanilovSoft.AsyncEx;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace XUnitTests
{
    public class ManualResetEventSourceTest
    {
        [Fact]
        public void Test2()
        {
            var mres = new ManualResetEventSource<int>();

            mres.Reset();
            mres.TrySet(123);
            mres.TryTake(out _);
            
            try
            {
                mres.TryTake(out _);
                Assert.True(false);
            }
            catch (InvalidOperationException)
            {
                
            }
        }
    }
}
