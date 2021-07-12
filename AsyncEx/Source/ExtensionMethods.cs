using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

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

        public static CancellationTokenRegistration UnsafeRegister(this CancellationToken cancellationToken, Action<object?> action, object? state)
        {
            return cancellationToken.Register(action, state, useSynchronizationContext: false);
        }
#endif

        public static void Remove<T>(this Queue<T> queue, T item) where T : class
        {
            int cycleAmount = queue.Count;

            for (int i = 0; i < cycleAmount; i++)
            {
                T pulledItem = queue.Dequeue();
                if (pulledItem != item)
                {
                    queue.Enqueue(pulledItem);
                }
            }
        }
    }
}
