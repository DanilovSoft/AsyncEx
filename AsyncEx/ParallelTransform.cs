namespace DanilovSoft.AsyncEx
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    public interface IParallelEnumerator<out TInput>
    {
        TInput Item { get; }
        IEnumerable<Aggregator<TResult>> Run<TIn, TOut, TResult>(IEnumerable<TIn> items, Func<TIn, Task<TOut>> func, Func<TIn, TOut, TResult> resultSelector);
    }

    public sealed class Aggregator<TOutput>
    {
        public Func<Task<TOutput>> Func { get; }
        internal TOutput Result { get; set; } = default!;

        public Aggregator(Func<Task<TOutput>> func)
        {
            Func = func;
        }
    }

    /// <summary>
    /// Конвеёйр для параллельно обработки данных.
    /// </summary>
    public sealed class ParallelTransform
    {
        private sealed class ParallelEnumerator<TInput> : IParallelEnumerator<TInput>
        {
            public TInput Item { get; private set; } = default!;

            internal void MoveNext(TInput item)
            {
                Item = item;
            }

            public IEnumerable<Aggregator<TRes>> Run<TIn, TOut, TRes>(IEnumerable<TIn> items, Func<TIn, Task<TOut>> func, Func<TIn, TOut, TRes> resultSelector)
            {
                return items.Select(input => new Aggregator<TRes>(async () =>
                {
                    var tOut = await func(input).ConfigureAwait(false); ;
                    return resultSelector(input, tOut);
                }));
            }
        }

        public static async Task<IList<TResult>> Run<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<Aggregator<TOutput>>> func,
            Func<TInput, IList<TOutput>, TResult> resultSelector,
            int maxDegreeOfParallelism)
        {
            List<TResult> list = new();
            var en = Enumerate(items, func, resultSelector, maxDegreeOfParallelism);

            await foreach (var item in en)
            {
                list.Add(item);
            }
            return list;
        }

        public static async IAsyncEnumerable<TResult> Enumerate<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<Aggregator<TOutput>>> func,
            Func<TInput, IList<TOutput>, TResult> resultSelector,
            int maxDegreeOfParallelism)
        {
            if (items is ICollection<TInput> col && col.Count == 0)
                yield break; // return Array.Empty<TResult>();

            var enumerator = new ParallelEnumerator<TInput>();

            List<(TInput Input, Aggregator<TOutput>[] Agg)> perInputAggr = new();

            // Получим от пользователя все агрегаторы и инстанцируем замыкания.
            foreach (TInput input in items)
            {
                enumerator.MoveNext(input);
                var funcs = func(enumerator);
                perInputAggr.Add((input, funcs.ToArray()));
            }

            var opt = new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = maxDegreeOfParallelism,
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = CancellationToken.None
            };

            var actionBlock = new ActionBlock<Aggregator<TOutput>>(async aggregator =>
            {
                aggregator.Result = await aggregator.Func().ConfigureAwait(false);
            }, opt);

            foreach (var input in perInputAggr)
            {
                foreach (var aggregator in input.Agg)
                {
                    if (!await actionBlock.SendAsync(aggregator).ConfigureAwait(false))
                    // Произошла ошибка внутри конвейера.
                    {
                        // Извлекаем исключение.
                        await actionBlock.Completion.ConfigureAwait(false);
                    }
                }
            }
            actionBlock.Complete();
            await actionBlock.Completion.ConfigureAwait(false);

            foreach (var child in perInputAggr)
            {
                TOutput[] outputArr = child.Agg.Select(x => x.Result).ToArray();
                TResult final = resultSelector(child.Input, outputArr);
                yield return final;
            }
        }

        public static async Task<IList<TResult>> Run<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func,
            Func<TInput, TOutput, TResult> selector,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken = default)
        {
            return (await Run(items, func, maxDegreeOfParallelism, cancellationToken))
                .Select(x => selector(x.Input, x.Output))
                .ToArray();
        }

        public static async Task<IList<(TInput Input, TOutput Output)>> Run<TInput, TOutput>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            if (items is ICollection<TInput> col && col.Count == 0)
                return Array.Empty<(TInput Input, TOutput Output)>();

            var opt = new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = maxDegreeOfParallelism,
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            var transformBlock = new TransformBlock<TInput, (TInput, TOutput)>(async input =>
            {
                var output = await func(input).ConfigureAwait(false);
                return (input, output);
            }, opt);

            var resultBuffer = new BufferBlock<(TInput Input, TOutput Output)>();

            transformBlock.LinkTo(resultBuffer, new DataflowLinkOptions { PropagateCompletion = true });

            foreach (var item in items)
            {
                if (!await transformBlock.SendAsync(item).ConfigureAwait(false))
                // Произошла ошибка внутри конвейера.
                {
                    // Извлекаем исключение.
                    await transformBlock.Completion.ConfigureAwait(false);
                }
            }
            transformBlock.Complete();
            await transformBlock.Completion.ConfigureAwait(false);

            if (resultBuffer.TryReceiveAll(out var outItems))
            {
                await resultBuffer.Completion.ConfigureAwait(false);
                return outItems;
            }
            else
            {
                //Debug.Assert(false);
                return Array.Empty<(TInput Input, TOutput Output)>();
            }
        }


        public static async Task Run<TInput>(IEnumerable<TInput> items,
            Func<TInput, Task> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            var opt = new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = maxDegreeOfParallelism,
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            var actionBlock = new ActionBlock<TInput>(input => func(input), opt);

            foreach (var item in items)
            {
                if (!await actionBlock.SendAsync(item).ConfigureAwait(false))
                // Произошла ошибка внутри конвейера.
                {
                    // Извлекаем исключение.
                    await actionBlock.Completion.ConfigureAwait(false);
                }
            }
            actionBlock.Complete();
            await actionBlock.Completion.ConfigureAwait(false);
        }
    }
}
