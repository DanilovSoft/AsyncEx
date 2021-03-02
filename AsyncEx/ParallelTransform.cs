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
    using DanilovSoft.Threading.Tasks;

    public interface IParallelEnumerator<out TInput>
    {
        TInput Item { get; }

        IEnumerable<IAggregator<TResult>> Sub<TIn, TOut, TResult>(
            IEnumerable<TIn> items,
            Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TOut>>> aggregateFunc,
            Func<TIn, IReadOnlyList<TOut>, TResult> resultSelector);

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
        private sealed class Aggregator<TOutput, TArg1, TArg2, TArg3> : IAggregator<TOutput>
        {
            private readonly Func<TArg1, TArg2, TArg3, Task<TOutput>> _asyncFunc;
            public TOutput Result { get; private set; } = default!;
            private readonly TArg1 _arg1;
            private readonly TArg2 _arg2;
            private readonly TArg3 _arg3;

            internal Aggregator(Func<TArg1, TArg2, TArg3, Task<TOutput>> func, TArg1 arg1, TArg2 arg2, TArg3 arg3)
            {
                _asyncFunc = func;
                _arg1 = arg1;
                _arg2 = arg2;
                _arg3 = arg3;
            }

            public Task InvokeAsync()
            {
                Task<TOutput> task;
                try
                {
                    task = _asyncFunc(_arg1, _arg2, _arg3);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }

                if (task.IsCompletedSuccessfully())
                {
                    Result = task.Result;
                    return Task.CompletedTask;
                }
                else
                {
                    return WaitAsync(task, this);

                    static async Task WaitAsync(Task<TOutput> task, Aggregator<TOutput, TArg1, TArg2, TArg3> self)
                    {
                        self.Result = await task.ConfigureAwait(false);
                    }
                }
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
                    yield return CreateAggregator(static (func, subItem, resultSelector) =>
                    {
                        Task<TOut> task;
                        try
                        {
                            task = func(subItem);
                        }
                        catch (Exception ex)
                        {
                            return Task.FromException<TRes>(ex);
                        }

                        if (task.IsCompletedSuccessfully())
                        {
                            TOut result = task.Result;
                            return Task.FromResult(resultSelector(subItem, result));
                        }
                        else
                        {
                            return WaitAsync(task, resultSelector, subItem);
                            static async Task<TRes> WaitAsync(Task<TOut> task, Func<TIn, TOut, TRes> resultSelector, TIn subItem)
                            {
                                TOut result = await task.ConfigureAwait(false);
                                return resultSelector(subItem, result);
                            }
                        }
                    }, func, subItem, resultSelector);
                }
            }

            public IEnumerable<IAggregator<TRes>> Sub<TIn, TInherim, TRes>(
                IEnumerable<TIn> subItems, 
                Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TInherim>>> aggregateFunc, 
                Func<TIn, IReadOnlyList<TInherim>, TRes> resultSelector)
            {
                foreach (TIn subItem in subItems)
                {
                    var en = new ParallelEnumerator<TIn>();
                    en.MoveNext(subItem);

                    IAggregator<TInherim>[] aggs = aggregateFunc(en).ToArray();

                    yield return CreateAggregator(static async (subItem, aggs, resultSelector) =>
                    {
                        TInherim[] outputs = new TInherim[aggs.Length];
                        for (int i = 0; i < aggs.Length; i++)
                        {
                            var ag = aggs[i];
                            await ag.InvokeAsync().ConfigureAwait(false);
                            outputs[i] = ag.Result;
                        }
                        return resultSelector(subItem, outputs);
                    }, subItem, aggs, resultSelector);
                }
            }
        }

        public static async Task<IReadOnlyList<TResult>> Transform<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<IAggregator<TOutput>>> func,
            Func<TInput, IReadOnlyList<TOutput>, TResult> resultSelector,
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
            Func<TInput, IReadOnlyList<TOutput>, TResult> resultSelector,
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
                var item = itemsAgg[i];
                for (int j = 0; j < item.Agg.Length; j++)
                {
                    if (!await actionBlock.SendAsync(item.Agg[j]).ConfigureAwait(false))
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

        public static async Task<IReadOnlyList<TResult>> Run<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func,
            Func<TInput, TOutput, TResult> selector,
            int maxDegreeOfParallelism = -1,
            CancellationToken cancellationToken = default)
        {
            var outItems = await Run(items, func, maxDegreeOfParallelism, cancellationToken).ConfigureAwait(false);

            return outItems
                .Select(x => selector(x.Input, x.Output))
                .ToArray();
        }

        public static async Task<IReadOnlyList<(TInput Input, TOutput Output)>> Run<TInput, TOutput>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            if (items is ICollection<TInput> col && col.Count == 0)
                return Array.Empty<(TInput Input, TOutput Output)>();

            var transformBlock = new TransformBlock<TInput, (TInput, TOutput)>(input => RunTransform(func, input), 
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
                return (IReadOnlyList<(TInput, TOutput)>)outItems;
            }
            else
            {
                return Array.Empty<(TInput Input, TOutput Output)>();
            }
        }

        private static Task<(TInput, TOutput)> RunTransform<TInput, TOutput>(Func<TInput, Task<TOutput>> func, TInput input)
        {
            Task<TOutput> task;
            try
            {
                task = func(input);
            }
            catch (Exception ex)
            {
                return Task.FromException<(TInput, TOutput)>(ex);
            }

            if (task.IsCompletedSuccessfully())
            {
                var output = task.Result;
                return Task.FromResult((input, output));
            }
            else
            {
                return WaitAsync(task, input);
                static async Task<(TInput, TOutput)> WaitAsync(Task<TOutput> task, TInput input)
                {
                    TOutput output = await task.ConfigureAwait(false);
                    return (input, output);
                }
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

        private static Aggregator<TOutput, TArg1, TArg2, TArg3> CreateAggregator<TOutput, TArg1, TArg2, TArg3>(
            Func<TArg1, TArg2, TArg3, Task<TOutput>> func,
            TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            return new Aggregator<TOutput, TArg1, TArg2, TArg3>(func, arg1, arg2, arg3);
        }
    }
}
