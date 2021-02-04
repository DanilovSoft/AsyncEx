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

        IEnumerable<IAggregator<TResult>> Sub<TIn, TOut, TResult>(
            IEnumerable<TIn> items,
            Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TOut>>> aggregateFunc,
            Func<TIn, IList<TOut>, TResult> resultSelector);

        IEnumerable<IAggregator<TResult>> Run<TIn, TOut, TResult>(
            IEnumerable<TIn> items, 
            Func<TIn, Task<TOut>> func, Func<TIn, TOut, TResult> resultSelector);
    }

    public interface IAggregator<TOutput>
    {
        Task InvokeAsync();
        TOutput Result { get; }
    }

    /// <summary>
    /// Конвейер для параллельной обработки данных.
    /// </summary>
    public sealed class ParallelTransform
    {
        private sealed class Aggregator<TOutput, TState> : IAggregator<TOutput>
        {
            private readonly Func<TState, Task<TOutput>> _asyncFunc;
            public TOutput Result { get; private set; } = default!;
            private readonly TState _state;

            internal Aggregator(Func<TState, Task<TOutput>> func, TState state)
            {
                _asyncFunc = func;
                _state = state;
            }

            public async Task InvokeAsync()
            {
                Result = await _asyncFunc(_state).ConfigureAwait(false);
            }
        }

        private sealed class ParallelEnumerator<TInput> : IParallelEnumerator<TInput>
        {
            public TInput Item { get; private set; } = default!;

            internal void MoveNext(TInput item)
            {
                Item = item;
            }

            public IEnumerable<IAggregator<TRes>> Run<TIn, TOut, TRes>(
                IEnumerable<TIn> subItems, 
                Func<TIn, Task<TOut>> func, 
                Func<TIn, TOut, TRes> resultSelector)
            {
                foreach (TIn subItem in subItems)
                {
                    yield return CreateAggregator(static async t =>
                    {
                        TOut result = await t.func(t.subItem).ConfigureAwait(false);
                        return t.resultSelector(t.subItem, result);
                    }, 
                    state: (func, subItem, resultSelector));
                }
            }

            public IEnumerable<IAggregator<TRes>> Sub<TIn, TInherim, TRes>(
                IEnumerable<TIn> subItems, 
                Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TInherim>>> aggregateFunc, 
                Func<TIn, IList<TInherim>, TRes> resultSelector)
            {
                foreach (TIn subItem in subItems)
                {
                    var en = new ParallelEnumerator<TIn>();
                    en.MoveNext(subItem);

                    IAggregator<TInherim>[] aggs = aggregateFunc(en).ToArray();

                    yield return CreateAggregator(static async t =>
                    {
                        List<TInherim> outputs = new(t.aggs.Length);
                        for (int i = 0; i < t.aggs.Length; i++)
                        {
                            var ag = t.aggs[i];
                            await ag.InvokeAsync().ConfigureAwait(false);
                            outputs.Add(ag.Result);
                        }
                        return t.resultSelector(t.subItem, outputs);
                    },
                    state: (subItem, aggs, resultSelector));
                }
            }
        }

        public static async Task<IList<TResult>> Transform<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<IAggregator<TOutput>>> func,
            Func<TInput, IList<TOutput>, TResult> resultSelector,
            int maxDegreeOfParallelism = -1)
        {
            List<TResult> list = new();
            IAsyncEnumerable<TResult> en = EnumerateAsync(items, func, resultSelector, maxDegreeOfParallelism);

            await foreach (var result in en.ConfigureAwait(false))
            {
                list.Add(result);
            }
            return list;
        }

        public static async IAsyncEnumerable<TResult> EnumerateAsync<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<IAggregator<TOutput>>> func,
            Func<TInput, IList<TOutput>, TResult> resultSelector,
            int maxDegreeOfParallelism)
        {
            if (items is ICollection<TInput> col && col.Count == 0)
                yield break;

            var enumerator = new ParallelEnumerator<TInput>();

            // Нужно обязательно агрегировать funcs что-бы повторно итерировать после конвейера.
            List<(TInput Input, IAggregator<TOutput>[] Agg)> itemsAgg = new();

            // Получим от пользователя все агрегаторы и инстанцируем замыкания.
            foreach (TInput input in items)
            {
                enumerator.MoveNext(input);
                var funcs = func(enumerator);
                itemsAgg.Add((input, funcs.ToArray()));
            }

            var actionBlock = new ActionBlock<IAggregator<TOutput>>(aggregator => aggregator.InvokeAsync(), new()
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = maxDegreeOfParallelism,
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = CancellationToken.None
            });

            for (int i = 0; i < itemsAgg.Count; i++)
            {
                for (int j = 0; j < itemsAgg[i].Agg.Length; j++)
                {
                    if (!await actionBlock.SendAsync(itemsAgg[i].Agg[j]).ConfigureAwait(false))
                    // Произошла ошибка внутри конвейера.
                    {
                        // Извлекаем исключение.
                        await actionBlock.Completion.ConfigureAwait(false);
                    }
                }
            }
            actionBlock.Complete();
            await actionBlock.Completion.ConfigureAwait(false);

            for (int i = 0; i < itemsAgg.Count; i++)
            {
                (TInput input, IAggregator<TOutput>[] agg) = itemsAgg[i];

                List<TOutput> outputs = new(agg.Length);

                for (int j = 0; j < agg.Length; j++)
                {
                    outputs.Add(agg[j].Result);
                }
                yield return resultSelector(input, outputs);
            }
        }

        public static async Task<IList<TResult>> Run<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func,
            Func<TInput, TOutput, TResult> selector,
            int maxDegreeOfParallelism = -1,
            CancellationToken cancellationToken = default)
        {
            return (await Run(items, func, maxDegreeOfParallelism, cancellationToken))
                .Select(x => selector(x.Input, x.Output))
                .ToList();
        }

        public static async Task<IList<(TInput Input, TOutput Output)>> Run<TInput, TOutput>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            if (items is ICollection<TInput> col && col.Count == 0)
                return Array.Empty<(TInput Input, TOutput Output)>();

            var transformBlock = new TransformBlock<TInput, (TInput, TOutput)>(async input =>
            {
                TOutput output = await func(input).ConfigureAwait(false);
                return (input, output);
            }, 
            new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = maxDegreeOfParallelism,
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            });

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
                return Array.Empty<(TInput Input, TOutput Output)>();
            }
        }


        public static async Task Run<TInput>(IEnumerable<TInput> items,
            Func<TInput, Task> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            var actionBlock = new ActionBlock<TInput>(input => func(input), new()
            {
                EnsureOrdered = false,
                SingleProducerConstrained = true,
                BoundedCapacity = maxDegreeOfParallelism,
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            });

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

        private static Aggregator<TOutput, TState> CreateAggregator<TOutput, TState>(
            Func<TState, Task<TOutput>> func, 
            TState state)
        {
            return new Aggregator<TOutput, TState>(func, state);
        }
    }
}
