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
        public void Test1()
        {
            var mres = new ManualResetEventSource<int>();

            try
            {
                int n = mres.Wait();
            }
            catch (InvalidOperationException)
            {

            }
            Assert.True(false);
        }

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
            }
            catch (InvalidOperationException)
            {

            }
            Assert.True(false);
        }
    }
}
