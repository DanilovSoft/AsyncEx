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
