using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    internal static class ThrowHelper
    {
        /// <exception cref="ArgumentNullException"/>
        public static void ArgumentNotNull<T>([NotNull] T? value, string paramName)
        {
            if (value is null)
            {
                ThrowArgumentNull(paramName);
            }
        }

        /// <exception cref="ArgumentNullException"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNull(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        /// <exception cref="ArgumentOutOfRangeException"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRange(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        /// <exception cref="ObjectDisposedException"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposed<T>()
        {
            throw new ObjectDisposedException(typeof(T).Name);
        }
    }
}
