using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    internal static class ThrowHelper
    {
        /// <exception cref="ArgumentOutOfRangeException"/>
        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        /// <exception cref="ObjectDisposedException"/>
        [DoesNotReturn]
        public static void ThrowObjectDisposed<T>()
        {
            throw new ObjectDisposedException(typeof(T).Name);
        }
    }
}
