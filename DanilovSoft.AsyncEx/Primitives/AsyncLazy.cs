using System;
using System.Diagnostics;
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
        private readonly Func<object, object?, Task<T>> _taskFactory;
        private readonly bool _cacheFailure;
        private readonly object? _state;
        private readonly object _publicFactory;
        private Task<T>? _task;
        private object SyncObj => _taskFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
        public AsyncLazy(Func<Task<T>> valueFactory, bool cacheFailure = true)
        {
            _publicFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _taskFactory = static (f, _) => ((Func<Task<T>>)f).Invoke();
            _cacheFailure = cacheFailure;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
        public AsyncLazy(Func<object?, Task<T>> valueFactory, object? state, bool cacheFailure = true)
        {
            _state = state;
            _publicFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _taskFactory = static (f, s) => ((Func<object?, Task<T>>)f).Invoke(s);
            _cacheFailure = cacheFailure;
        }

        ///// <summary>
        ///// The underlying lazy task.
        ///// </summary>
        //private volatile Lazy<Task<T>> _lazy;

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Value
        {
            [DebuggerStepThrough]
            get => GetValueAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        ///  Gets a value that indicates whether a value has been created for this <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        public bool IsValueCreated
        {
            get
            {
                var task = _task;
                return task != null && task.IsCompletedSuccessfully();
            }
        }

        /// <summary>
        /// Gets the value of the <see cref="AsyncLazy{T}"/> for debugging display purposes.
        /// </summary>
        private T? ValueForDebugDisplay
        {
            get
            {
                var task = _task;
                if (task != null && task.IsCompletedSuccessfully())
                {
                    return task.Result;
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
                var task = _task;
                return task != null && (task.IsFaulted || task.IsCanceled);
            }
        }

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        public Task<T> GetValueAsync()
        {
            lock (SyncObj)
            {
                if (_task == null || !_cacheFailure && (_task.IsFaulted || _task.IsCanceled))
                {
                    try
                    {
                        // Тригерим запуск асинхронной операции.
                        _task = _taskFactory.Invoke(_publicFactory, _state);
                    }
                    catch (Exception ex)
                    {
                        if (_cacheFailure)
                        {
                            _task = Task.FromException<T>(ex);
                        }
                        else
                        {
                            _task = null;
                            return Task.FromException<T>(ex);
                        }
                    }

                    if (!_cacheFailure && (_task.IsFaulted || _task.IsCanceled))
                    {
                        var task = _task;
                        _task = null;
                        return task;
                    }
                }
                return _task;
            }
        }

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            ThreadPool.UnsafeQueueUserWorkItem(static s =>
            {
                s.GetValueAsync().ObserveException();

            }, this, preferLocal: true);
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

            //public bool IsStarted => _self.IsStarted;

            public bool IsValueCreated => _self.IsValueCreated;

            public T? Value => _self.ValueForDebugDisplay;

            public bool IsValueFaulted => _self.IsValueFaulted;
        }
    }
}