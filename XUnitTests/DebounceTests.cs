using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DanilovSoft.AsyncEx;
using Xunit;

namespace XUnitTests
{
    public class DebounceTests
    {
        [Fact]
        public void DelayMethod_Success()
        {
            int controlValue = -1;

            void SomeAction(int value)
            {
                controlValue = value;
            }

            var debounce = new Debounce<int>(SomeAction, TimeSpan.FromMilliseconds(500));
            
            debounce.Invoke(1);
            debounce.Invoke(2);
            debounce.Invoke(3);

            Thread.Sleep(600);
            
            Assert.Equal(3, controlValue);
        }

        [Fact]
        public void CancelAndWait()
        {
            int controlValue = -1;

            void SomeAction(int value)
            {
                controlValue = value;
                Thread.Sleep(1000);
            }

            var debounce = new Debounce<int>(SomeAction, TimeSpan.FromMilliseconds(500));
            try
            {
                debounce.Invoke(1);
                debounce.Invoke(2);
                debounce.Invoke(3);
                Thread.Sleep(600);
            }
            finally
            {
                debounce.Cancel();
            }

            Assert.Equal(3, controlValue);
        }

        [Fact]
        public void Cancel_Success()
        {
            int controlValue = -1;

            void SomeAction(int value)
            {
                controlValue = value;
            }

            var debounce = new Debounce<int>(SomeAction, TimeSpan.FromMilliseconds(500));
            try
            {
                debounce.Invoke(1);
                debounce.Invoke(2);
                debounce.Invoke(3);
                Thread.Sleep(200);
            }
            finally
            {
                debounce.Cancel();
            }

            Thread.Sleep(600);

            Assert.Equal(-1, controlValue);
        }
    }
}
