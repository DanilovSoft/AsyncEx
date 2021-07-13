using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public interface IParallelEnumerator<out TInput>
    {
        TInput Item { get; }

        IEnumerable<IAggregator<TResult>> SubQuery<TIn, TOut, TResult>(
            IEnumerable<TIn> items,
            Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TOut>>> aggregateFunc,
            Func<TIn, TOut[], TResult> resultSelector);

        IEnumerable<IAggregator<TResult>> Run<TIn, TOut, TResult>(
            IEnumerable<TIn> items, 
            Func<TIn, Task<TOut>> func, Func<TIn, TOut, TResult> resultSelector);
    }
}
