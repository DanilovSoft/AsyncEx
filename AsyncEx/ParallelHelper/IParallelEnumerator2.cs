using System.Collections.Generic;

namespace DanilovSoft.AsyncEx
{
    public interface IParallelEnumerator2<out TInput, out TInput2>
    {
        TInput Parent { get; }
        IAsyncEnumerator<TInput2> Items { get; }

        //IEnumerable<IAggregator<TResult>> Sub<TIn, TOut, TResult>(
        //    IEnumerable<TIn> items,
        //    Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TOut>>> aggregateFunc,
        //    Func<TIn, IReadOnlyList<TOut>, TResult> resultSelector);

        //IEnumerable<IAggregator<TResult>> Run<TIn, TOut, TResult>(
        //    IEnumerable<TIn> items,
        //    Func<TIn, Task<TOut>> func, Func<TIn, TOut, TResult> resultSelector);
    }
}
