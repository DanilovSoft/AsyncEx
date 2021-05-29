using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
    /// </summary>
    /// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
    [DebuggerTypeProxy(typeof(AsyncLazy<>.System_LazyDebugView))]
    [DebuggerDisplay(@"\{IsValueCreated = {IsValueCreated}\}")]
    public sealed class AsyncLazy<T>
    {
        private readonly bool _cacheFailure;
        private readonly Func<Task<T>> _valueFactory;

        /// <summary>
        /// The underlying lazy task.
        /// </summary>
        private volatile Lazy<Task<T>> _lazy;

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Value
        {
            [DebuggerStepThrough]
            get => _lazy.Value.GetAwaiter().GetResult();
        }

        /// <summary>
        ///  Gets a value that indicates whether a value has been created for this <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        public bool IsValueCreated
        {
            get
            {
                var lazy = _lazy;

                if (lazy.IsValueCreated)
                // Таск уже создан.
                {
                    Task<T> task = lazy.Value;
                    return task.Status == TaskStatus.RanToCompletion;
                }
                return false;
            }
        }
        /// <summary>
        /// Позволяет узнать была ли запущена асинхронная операция.
        /// </summary>
        private bool IsStarted => _lazy.IsValueCreated;

        /// <summary>
        /// Gets the value of the <see cref="AsyncLazy{T}"/> for debugging display purposes.
        /// </summary>
        private T? ValueForDebugDisplay
        {
            get
            {
                var lazy = _lazy;

                if (lazy.IsValueCreated)
                // Таск уже создан.
                {
                    Task<T> task = lazy.Value;
                    if (task.Status == TaskStatus.RanToCompletion)
                    // Таск уже успешно завершен.
                    {
                        return task.GetAwaiter().GetResult();
                    }
                }
                return default;
            }
        }

        /// <summary>
        /// Gets whether the value creation is faulted or not.
        /// </summary>
        private bool IsValueFaulted
        {
            get 
            {
                var lazy = _lazy;

                if (lazy.IsValueCreated)
                // Таск уже создан.
                {
                    Task<T> task = lazy.Value;
                    return task.IsFaulted || task.IsCanceled;
                }
                return false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        public AsyncLazy(Func<Task<T>> valueFactory) : this(valueFactory, cacheFailure: true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
        public AsyncLazy(Func<Task<T>> valueFactory, bool cacheFailure)
        {
            _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _cacheFailure = cacheFailure;
            _lazy = CreateLazy(valueFactory);
        }

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        public Task<T> GetValueAsync()
        {
            var lazy = _lazy;
            bool firstTry = true;

        RetryOnFailure:

            Task<T> task;
            try
            {
                // Тригерим запуск асинхронной операции.
                task = lazy.Value;
            }
            catch (Exception ex)
            {
                if (!_cacheFailure)
                {
                    Swap(lazy);
                }
                return Task.FromException<T>(ex);
            }

            if (!_cacheFailure && firstTry && task is { IsFaulted: true } or { IsCanceled: true })
            {
                firstTry = false;
                lazy = Swap(lazy);
                goto RetryOnFailure;
            }

            // Таск ещё не завершился или завершился с ошибкой.
            return task;
        }

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            try
            {
                // Тригерим запуск асинхронной операции.
                _ = _lazy.Value; // Может быть исключение в синхронной части метода фабрики.
            }
            catch { }
        }

        private Lazy<Task<T>> Swap(Lazy<Task<T>> lazy)
        {
            var newLazy = CreateLazy(_valueFactory);
            var cas = Interlocked.CompareExchange(ref _lazy, newLazy, lazy);

            return cas == lazy ? newLazy : cas;
        }

        private static Lazy<Task<T>> CreateLazy(Func<Task<T>> valueFactory)
        {
            // Гарантируем однократный запуск асинхронной операции.
            var newLazy = new Lazy<Task<T>>(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
            return newLazy;
        }

        /// <summary>A debugger view of the <see cref="AsyncLazy{T}"/> to surface additional debugging properties and 
        /// to ensure that the <see cref="AsyncLazy{T}"/> does not become initialized if it was not already.
        /// </summary>
        private sealed class System_LazyDebugView
        {
            private readonly AsyncLazy<T> _self;

            public System_LazyDebugView(AsyncLazy<T> self)
            {
                _self = self;
            }

            public bool IsStarted => _self.IsStarted;
            
            public bool IsValueCreated => _self.IsValueCreated;
            
            public T? Value => _self.ValueForDebugDisplay;
            
            public bool IsValueFaulted => _self.IsValueFaulted;
        }
    }
}
