using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public static class TaskHelper
    {
        /// <remarks>Проглатывает последующие исключения.</remarks>
        [DebuggerStepThrough]
        public static Task WhenAllOrAnyException(params Task[] tasksArray)
        {
            return WhenAllOrAnyException(tasks: tasksArray);
        }

        /// <remarks>Проглатывает последующие исключения.</remarks>
        [DebuggerStepThrough]
        public static async Task WhenAllOrAnyException(IEnumerable<Task> tasks)
        {
            var list = tasks.ToList();
            while (list.Count > 0)
            {
                var completedTask = await Task.WhenAny(list).ConfigureAwait(false);
                list.Remove(completedTask);

                if (completedTask.Exception?.InnerException is Exception ex)
                {
                    foreach (var task in list)
                    {
                        // "Просмотрим" любые исключения и проигнорируем их, что-бы предотвратить событие UnobservedTaskException.
                        _ = task.ContinueWith(static t => { _ = t.Exception; },
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                    }

                    // Остальные таски будут брошены, а исключения проглочены.
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Шорткат для Task.Run(() =>
        /// </summary>
        [DebuggerStepThrough]
        public static Task<TResult> Run<T, TResult>(Func<T, Task<TResult>> func, T arg)
        {
            // Аналогично Task.Run(() => func(arg)) но без замыкания.

            return Task.Factory.StartNew(static s =>
            {
                var tuple = (Tuple<Func<T, Task<TResult>>, T>)s!;
                return tuple.Item1(tuple.Item2);

            }, Tuple.Create(func, arg), CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .Unwrap();

            //return Task.Run(() => func(arg));
        }

        public static Task<TResult> Run<TResult>(Func<Task<TResult>> func)
        {
            return Task.Run(() => func());
        }
    }
}
