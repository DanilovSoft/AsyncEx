using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx;

/// <summary>
/// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
/// </summary>
/// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
[DebuggerTypeProxy(typeof(AsyncLazy<>.System_LazyDebugView))]
[DebuggerDisplay(@"\{IsValueCreated = {IsValueCreated}\}")]
public sealed class AsyncLazy<T>
{
    private readonly bool _cacheFailure;
    private readonly Func<object, object?, CancellationToken, Task<T>> _taskFactory;
    private readonly object? _state;
    private readonly object _publicFactory;
    private Task<T>? _lastTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
    /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
    public AsyncLazy(Func<Task<T>> valueFactory, bool cacheFailure = true)
    {
        _publicFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        _taskFactory = static (f, _, _) => ((Func<Task<T>>)f).Invoke();
        _cacheFailure = cacheFailure;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
    /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
    public AsyncLazy(Func<CancellationToken, Task<T>> valueFactory, bool cacheFailure = true)
    {
        _publicFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        _taskFactory = static (f, _, ct) => ((Func<CancellationToken, Task<T>>)f).Invoke(ct);
        _cacheFailure = cacheFailure;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
    /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
    public AsyncLazy(object? state, Func<object?, Task<T>> valueFactory, bool cacheFailure = true)
    {
        _publicFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        _taskFactory = static (f, s, _) => ((Func<object?, Task<T>>)f).Invoke(s);
        _cacheFailure = cacheFailure;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
    /// </summary>
    /// <param name="valueFactory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed.</param>
    /// <param name="cacheFailure">If <see langword="false"/> then if the factory method fails, then re-run the factory method the next time instead of caching the failed task.</param>
    public AsyncLazy(object? state, Func<object?, CancellationToken, Task<T>> valueFactory, bool cacheFailure = true)
    {
        _publicFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        _taskFactory = static (f, s, ct) => ((Func<object?, CancellationToken, Task <T>>)f).Invoke(s, ct);
        _cacheFailure = cacheFailure;
    }

    private object SyncObj => _taskFactory;

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
        get
        {
            throw new NotImplementedException();
            //return _lazy.Value.GetAwaiter().GetResult();
        }
    }

    /// <summary>
    ///  Gets a value that indicates whether a value has been created for this <see cref="AsyncLazy{T}"/> instance.
    /// </summary>
    public bool IsValueCreated
    {
        get
        {
            throw new NotImplementedException();
            //var lazy = _lazy;

            //if (lazy.IsValueCreated)
            //// Таск уже создан.
            //{
            //    Task<T> task = lazy.Value;
            //    return task.Status == TaskStatus.RanToCompletion;
            //}
            //return false;
        }
    }
    /// <summary>
    /// Позволяет узнать была ли запущена асинхронная операция.
    /// </summary>
    private bool IsStarted
    {
        get
        {
            throw new NotImplementedException();
            //return _lazy.IsValueCreated;
        }
    }

    /// <summary>
    /// Gets the value of the <see cref="AsyncLazy{T}"/> for debugging display purposes.
    /// </summary>
    private T? ValueForDebugDisplay
    {
        get
        {
            throw new NotImplementedException();
            //var lazy = _lazy;

            //if (lazy.IsValueCreated)
            //// Таск уже создан.
            //{
            //    Task<T> task = lazy.Value;
            //    if (task.Status == TaskStatus.RanToCompletion)
            //    // Таск уже успешно завершен.
            //    {
            //        return task.GetAwaiter().GetResult();
            //    }
            //}
            //return default;
        }
    }

    /// <summary>
    /// Gets whether the value creation is faulted or not.
    /// </summary>
    private bool IsValueFaulted
    {
        get
        {
            throw new NotImplementedException();
            //var lazy = _lazy;

            //if (lazy.IsValueCreated)
            //// Таск уже создан.
            //{
            //    Task<T> task = lazy.Value;
            //    return task.IsFaulted || task.IsCanceled;
            //}
            //return false;
        }
    }

    /// <summary>
    ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
    /// </summary>
    public Task<T> GetValueAsync()
    {
        return GetValueAsync(CancellationToken.None);
    }

    /// <summary>
    ///  Gets the lazily initialized value of the current <see cref="AsyncLazy{T}"/> instance.
    /// </summary>
    public Task<T> GetValueAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        lock (SyncObj)
        {
            if (_lastTask == null || !_cacheFailure && (_lastTask.IsFaulted || _lastTask.IsCanceled))
            {
                try
                {
                    // Тригерим запуск асинхронной операции.
                    _lastTask = _taskFactory.Invoke(_publicFactory, _state, cancellationToken);
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    if (_cacheFailure)
                    {
                        _lastTask = Task.FromCanceled<T>(cancellationToken);
                    }
                    else
                    {
                        _lastTask = null;
                        return Task.FromCanceled<T>(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    if (_cacheFailure)
                    {
                        _lastTask = Task.FromException<T>(ex);
                    }
                    else
                    {
                        _lastTask = null;
                        return Task.FromException<T>(ex);
                    }
                }
            }
            return _lastTask;
        }
    }

    /// <summary>
    /// Starts the asynchronous initialization, if it has not already started.
    /// </summary>
    public void Start()
    {
        try
        {
            throw new NotImplementedException();

            // Тригерим запуск асинхронной операции.
            //_ = _lazy.Value; // Может быть исключение в синхронной части метода фабрики.
        }
        catch { }
    }

    private static Func<object?, CancellationToken, Task<T>> Call(Func<Task<T>> valueFactory)
    {
        throw new NotImplementedException();
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
