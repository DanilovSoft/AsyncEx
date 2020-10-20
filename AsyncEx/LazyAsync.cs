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
    [DebuggerTypeProxy(typeof(LazyAsync<>.System_LazyDebugView))]
    [DebuggerDisplay(@"\{IsValueCreated = {IsValueCreated}\}")]
    public sealed class LazyAsync<T>
    {
        /// <summary>
        /// The underlying lazy task.
        /// </summary>
        private readonly Lazy<Task<T>> _lazy;

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="LazyAsync{T}"/> instance.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Value
        {
            [DebuggerStepThrough]
            get => _lazy.Value.GetAwaiter().GetResult();
        }

        /// <summary>
        ///  Gets a value that indicates whether a value has been created for this <see cref="LazyAsync{T}"/> instance.
        /// </summary>
        public bool IsValueCreated
        {
            get
            {
                if (_lazy.IsValueCreated)
                // Таск уже создан.
                {
                    Task<T> task = _lazy.Value;
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
        /// Gets the value of the <see cref="LazyAsync{T}"/> for debugging display purposes.
        /// </summary>
        [MaybeNull]
        private T ValueForDebugDisplay
        {
            get
            {
                if (_lazy.IsValueCreated)
                // Таск уже создан.
                {
                    Task<T> task = _lazy.Value;
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
                if (_lazy.IsValueCreated)
                // Таск уже создан.
                {
                    Task<T> task = _lazy.Value;
                    return task.IsFaulted || task.IsCanceled;
                }
                return false;
            }
        }

        //private bool IsCanceled
        //{
        //    get
        //    {
        //        if (_lazy.IsValueCreated)
        //        // Таск уже создан.
        //        {
        //            Task<T> task = _lazy.Value;
        //            return task.IsCanceled;
        //        }
        //        return false;
        //    }
        //}

        //private bool IsCompleted
        //{
        //    get
        //    {
        //        if (_lazy.IsValueCreated)
        //        // Таск уже создан.
        //        {
        //            Task<T> task = _lazy.Value;
        //            return task.IsCompleted;
        //        }
        //        return false;
        //    }
        //}

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyAsync{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        public LazyAsync(Func<Task<T>> valueFactory)
        {
            // Гарантируем однократный запуск асинхронной операции.
            _lazy = new Lazy<Task<T>>(valueFactory: valueFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="LazyAsync{T}"/> instance.
        /// </summary>
        public ValueTask<T> GetValueAsync()
        {
            // Тригерим запуск асинхронной операции.
            Task<T> task = _lazy.Value;

            if (task.Status == TaskStatus.RanToCompletion)
            {
                return new ValueTask<T>(result: task.Result);
            }
            else
            // Таск завершился с ошибкой.
            {
                return new ValueTask<T>(task: task);
            }
        }

        //public bool GetValueOrStart(out T value)
        //{
        //    // Тригерим запуск асинхронной операции.
        //    Task<T> task = _lazy.Value;

        //    if (task.IsCompleted)
        //    // Таск завершен (не факт что успешно).
        //    {
        //        // Может быть исключение.
        //        value = task.GetAwaiter().GetResult();
        //        return true;
        //    }
        //    else
        //    {
        //        value = default;
        //        return false;
        //    }
        //}

        //public bool TryGetValue(out T value)
        //{
        //    if (_lazy.IsValueCreated)
        //    // Таск уже создан.
        //    {
        //        Task<T> task = _lazy.Value;

        //        if (task.IsCompleted)
        //        // Таск завершен (не факт что успешно).
        //        {
        //            // Может быть исключение.
        //            value = task.GetAwaiter().GetResult();
        //            return true;
        //        }
        //        else
        //        {
        //            value = default;
        //            return false;
        //        }
        //    }
        //    else
        //    // Таск ещё не запущен.
        //    {
        //        value = default;
        //        return false;
        //    }
        //}

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            // Тригерим запуск асинхронной операции.
            _ = _lazy.Value;
        }

        /// <summary>A debugger view of the <see cref="LazyAsync{T}"/> to surface additional debugging properties and 
        /// to ensure that the <see cref="LazyAsync{T}"/> does not become initialized if it was not already.
        /// </summary>
        private sealed class System_LazyDebugView
        {
            private readonly LazyAsync<T> _self;

            public System_LazyDebugView(LazyAsync<T> self)
            {
                _self = self;
            }

            public bool IsStarted => _self.IsStarted;
            //public bool IsCompleted => _self.IsCompleted;
            public bool IsValueCreated => _self.IsValueCreated;
            [MaybeNull]
            public T Value => _self.ValueForDebugDisplay;
            //public bool IsCanceled => _self.IsCanceled;
            public bool IsValueFaulted => _self.IsValueFaulted;
        }
    }
}
