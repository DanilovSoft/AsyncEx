using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DanilovSoft.AsyncEx
{
    /// <summary>
    /// Конвейер для параллельной обработки данных.
    /// </summary>
    public sealed class ParallelTransform
    {
        private sealed class Aggregator<TOutput, TFunc, TSubItem, TSelector> : IAggregator<TOutput>
        {
            private readonly Func<TFunc, TSubItem, TSelector, Task<TOutput>> _asyncFunc;
            public TOutput InvokeResult { get; private set; } = default!;
            private readonly TFunc _subFunc;
            private readonly TSubItem _subItem;
            private readonly TSelector _selector;

            internal Aggregator(Func<TFunc, TSubItem, TSelector, Task<TOutput>> func, TFunc subFunc, TSubItem subItem, TSelector selector)
            {
                _asyncFunc = func;
                _subFunc = subFunc;
                _subItem = subItem;
                _selector = selector;
            }

            public Task InvokeAsync()
            {
                Task<TOutput> task;
                try
                {
                    task = _asyncFunc(_subFunc, _subItem, _selector);
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }

                if (task.IsCompletedSuccessfully)
                {
                    InvokeResult = task.Result;
                    return Task.CompletedTask;
                }

                return WaitAsync(task);
                async Task WaitAsync(Task<TOutput> task)
                {
                    InvokeResult = await task.ConfigureAwait(false);
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
                foreach (var subItem in subItems)
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

                        if (task.IsCompletedSuccessfully)
                        {
                            var result = task.Result;
                            return Task.FromResult(resultSelector(subItem, result));
                        }
                        
                        return Wait(task, resultSelector, subItem);
                        static async Task<TRes> Wait(Task<TOut> task, Func<TIn, TOut, TRes> resultSelector, TIn subItem)
                        {
                            var result = await task.ConfigureAwait(false);
                            return resultSelector(subItem, result);
                        }
                    }, func, subItem, resultSelector);
                }
            }

            public IEnumerable<IAggregator<TRes>> SubQuery<TIn, TInherim, TRes>(
                IEnumerable<TIn> subItems, 
                Func<IParallelEnumerator<TIn>, IEnumerable<IAggregator<TInherim>>> aggregateFunc, 
                Func<TIn, TInherim[], TRes> resultSelector)
            {
                foreach (var subItem in subItems)
                {
                    var en = new ParallelEnumerator<TIn>();
                    en.MoveNext(subItem);

                    var aggs = aggregateFunc(en).ToArray();

                    yield return CreateAggregator(static async (subItem, aggs, resultSelector) =>
                    {
                        var outputs = new TInherim[aggs.Length];
                        for (var i = 0; i < aggs.Length; i++)
                        {
                            var ag = aggs[i];
                            await ag.InvokeAsync().ConfigureAwait(false);
                            outputs[i] = ag.InvokeResult;
                        }
                        return resultSelector(subItem, outputs);
                    }, subItem, aggs, resultSelector);
                }
            }
        }

        public static async Task<TResult[]> Transform<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<IAggregator<TOutput>>> func,
            Func<TInput, TOutput[], TResult> resultSelector,
            int maxDegreeOfParallelism = -1)
        {
            var list = new List<TResult>();
            var en = EnumerateAsync(items, func, resultSelector, maxDegreeOfParallelism);

            await foreach (var result in en.ConfigureAwait(false))
            {
                list.Add(result);
            }
            return list.ToArray();
        }

        public static async IAsyncEnumerable<TResult> EnumerateAsync<TInput, TOutput, TResult>(IEnumerable<TInput> items,
            Func<IParallelEnumerator<TInput>, IEnumerable<IAggregator<TOutput>>> func,
            Func<TInput, TOutput[], TResult> resultSelector,
            int maxDegreeOfParallelism)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            if (resultSelector == null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }

            if (items is ICollection<TInput> { Count: 0 })
            {
                yield break;
            }

            var enumerator = new ParallelEnumerator<TInput>();

            // Нужно обязательно агрегировать funcs что-бы повторно итерировать после конвейера.
            List<(TInput Input, IAggregator<TOutput>[] Agg)> itemsAgg = new();

            // Получим от пользователя все агрегаторы и инстанцируем замыкания.
            foreach (var input in items)
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

            for (var i = 0; i < itemsAgg.Count; i++)
            {
                var item = itemsAgg[i];
                for (var j = 0; j < item.Agg.Length; j++)
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

            for (var i = 0; i < itemsAgg.Count; i++)
            {
                (var input, var agg) = itemsAgg[i];

                var outputs = new TOutput[agg.Length];

                for (var j = 0; j < agg.Length; j++)
                {
                    outputs[j] = agg[j].InvokeResult;
                }
                yield return resultSelector(input, outputs);
            }
        }

        public static async Task<TResult[]> Run<TInput, TOutput, TResult>(IEnumerable<TInput> items,
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

        public static async Task<(TInput Input, TOutput Output)[]> Run<TInput, TOutput>(IEnumerable<TInput> items,
            Func<TInput, Task<TOutput>> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items is ICollection<TInput> { Count: 0 })
            {
                return Array.Empty<(TInput Input, TOutput Output)>();
            }

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
                return outItems.ToArray();
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

            if (task.IsCompletedSuccessfully)
            {
                var output = task.Result;
                return Task.FromResult((input, output));
            }
            else
            {
                return WaitAsync(task, input);
                static async Task<(TInput, TOutput)> WaitAsync(Task<TOutput> task, TInput input)
                {
                    var output = await task.ConfigureAwait(false);
                    return (input, output);
                }
            }
        }

        public static async Task Run<TInput>(IEnumerable<TInput> items,
            Func<TInput, Task> func, int maxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
            CancellationToken cancellationToken = default)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

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

        private static Aggregator<TOutput, TFunc, TSubItem, TSelector> CreateAggregator<TOutput, TFunc, TSubItem, TSelector>(
            Func<TFunc, TSubItem, TSelector, Task<TOutput>> func,
            TFunc subFunc, TSubItem subItem, TSelector selector)
        {
            return new Aggregator<TOutput, TFunc, TSubItem, TSelector>(func, subFunc, subItem, selector);
        }
    }
}
