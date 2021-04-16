using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DanilovSoft.AsyncEx
{
    internal static class ExtensionMethods
    {
#if NETSTANDARD2_0
        public static bool TryDequeue<T>(this Queue<T> queue, [MaybeNullWhen(false)] out T result)
        {
            if (queue.Count > 0)
            {
                result = queue.Dequeue();
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
#endif
    }
}
