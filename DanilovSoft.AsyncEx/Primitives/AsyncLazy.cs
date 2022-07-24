using DanilovSoft.AsyncEx.Helpers;
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
    [DebuggerTypeProxy(typeof(AsyncLazy<>.LazyDebugView))]
    [DebuggerDisplay(@"\{IsValueCreated = {IsValueCreated}\}")]
    public sealed class AsyncLazy<T>
    {
        /// <summary>
        /// Для манипуляций над <see cref="_task"/>.
        /// </summary>
        private readonly object _syncObj = new();
        private readonly Func<object?, CancellationToken, Task<T>> _taskFactory;
        private readonly bool _cacheFailure;
        private readonly object? _state;
        /// <summary>
        /// Любые манипуляции через блокировку <see cref="_syncObj"/>.
        /// </summary>
        private Task<T>? _task;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
        public AsyncLazy(Func<object?, CancellationToken, Task<T>> valueFactory, bool cacheFailure = true) : this(valueFactory, state: null, cacheFailure) {}

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
        /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
        public AsyncLazy(Func<object?, CancellationToken, Task<T>> valueFactory, object? state, bool cacheFailure = true)
        {
            _taskFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _state = state;
            _cacheFailure = cacheFailure;
        }

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Value => GetValueAsync().GetAwaiter().GetResult();

        /// <summary>
        ///  Gets a value that indicates whether a value has been created for this <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        public bool IsValueCreated
        {
            get
            {
                var task = _task;
                return task != null && task.IsCompletedSuccessfully;
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
                if (task != null && task.IsCompletedSuccessfully)
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
                if (!_cacheFailure)
                {
                    return false;
                }
                else
                {
                    var task = _task;
                    return task != null && (task.IsFaulted || task.IsCanceled);
                }
            }
        }

        /// <summary>
        ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
        /// </summary>
        public Task<T> GetValueAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncObj)
            {
                return GetValueCore(cancellationToken);
            }
        }

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            ThreadPool.QueueUserWorkItem(static s =>
            {
                s.GetValueAsync(CancellationToken.None).ObserveException();

            }, this, preferLocal: true);
        }

        private Task<T> GetValueCore(CancellationToken cancellationToken)
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));

            if (_task is null || _task.IsCanceled || !_cacheFailure && _task.IsFaulted)
            {
                try
                {
                    _task = _taskFactory(_state, cancellationToken); // Тригерим запуск асинхронной операции.
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return FactorySyncError(ex);
                }

                return AfterFactory();
            }
            else
            {
                return FromExistedTask(cancellationToken);
            }
        }

        private Task<T> FactorySyncError(Exception ex)
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));

            if (_cacheFailure)
            {
                _task = Task.FromException<T>(ex);
                return _task;
            }
            else
            {
                _task = null;
                return Task.FromException<T>(ex);
            }
        }

        private Task<T> AfterFactory()
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));
            Debug.Assert(_task != null);

            var task = _task;

            if (task.IsCanceled || (!_cacheFailure && task.IsFaulted))
            {
                _task = null;
            }

            return task;
        }

        private Task<T> FromExistedTask(CancellationToken cancellationToken)
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));
            Debug.Assert(_task != null);

            if (_task.IsCompletedSuccessfully || _task.IsFaulted)
            {
                return _task;
            }
            else
            {
                return WaitAsync(_task, cancellationToken).Unwrap();
            }
        }

        private async Task<Task<T>> WaitAsync(Task<T> task, CancellationToken cancellationToken)
        {
            try
            {
                await task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return task;
            }
            catch when (task.IsCanceled)
            {
                return RetryFactory(task, cancellationToken);
            }
        }

        private Task<T> RetryFactory(Task<T> task, CancellationToken cancellationToken)
        {
            lock (_syncObj)
            {
                if (_task == task)
                {
                    _task = null;
                    return GetValueCore(cancellationToken);
                }
            }

            return GetValueAsync(cancellationToken);
        }

        /// <summary>A debugger view of the <see cref="AsyncLazy{T}"/> to surface additional debugging properties and 
        /// to ensure that the <see cref="AsyncLazy{T}"/> does not become initialized if it was not already.
        /// </summary>
        private sealed class LazyDebugView
        {
            private readonly AsyncLazy<T> _thisRef;

            public LazyDebugView(AsyncLazy<T> thisRef)
            {
                _thisRef = thisRef;
            }

            public bool IsValueCreated => _thisRef.IsValueCreated;

            public T? Value => _thisRef.ValueForDebugDisplay;

            public bool IsValueFaulted => _thisRef.IsValueFaulted;
        }
    }
}