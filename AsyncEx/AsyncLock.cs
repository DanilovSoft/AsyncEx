using DanilovSoft.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    [DebuggerTypeProxy(typeof(DebugView))]
    [DebuggerDisplay("Taken = {" + nameof(DebugDisplay) + "}")]
    public sealed class AsyncLock
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool DebugDisplay => Volatile.Read(ref _taken) == 1;

        // ��� ���������� ������ � ������� � �������� �� �������.
        private readonly object _syncObj = new object();

        /// <summary>
        /// ������� ���������������� ������, ������� ����� �������� ����������.
        /// </summary>
        /// <remarks>������ ����� ���������� <see cref="_syncObj"/></remarks>
        private readonly WaitQueue _queue;

        /// <summary>
        /// ����� ��� ������ � �������� ���� ����� ���������� ����������.
        /// ����� ������ �������������.
        /// </summary>
        /// <remarks>������������ ������ �� ������������ ���������� ����� �������.</remarks>
        internal short _releaseTaskToken;

        /// <summary>
        /// ����� ���������� ��������� ������.
        /// </summary>
        /// <remarks>����������� ����� ���������� <see cref="_syncObj"/> ��� ��������.</remarks>
        private int _taken;

        public AsyncLock()
        {
            _queue = new WaitQueue(this);
        }

        /// <summary>
        /// ��������� ���������� ������ (Task), ��� ������ ���������� ����� LockAsync ����� ���������� ���������������
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public Task LockAsync(Func<Task> asyncAction)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (asyncAction != null)
            {
                ValueTask<LockReleaser> lockTask = LockAsync();

                if (lockTask.IsCompletedSuccessfully) // ���������� ��������� ���������.
                {
                    IDisposable? releaser = lockTask.Result;
                    try
                    {
                        Task userTask = asyncAction();

                        if (userTask.IsCompletedSuccessfully()) // ���������������� ����� ���������� ���������.
                        {
                            return Task.CompletedTask; // ��������� ����������.
                        }
                        else // ����� ����� ���������������� �����.
                        {
                            IDisposable releaserCopy = releaser;

                            // ������������� ��������������� Dispose.
                            releaser = null;

                            return WaitUserActionAndRelease(userTask, releaserCopy);

                            static async Task WaitUserActionAndRelease(Task userTask, IDisposable releaser)
                            {
                                try
                                {
                                    await userTask.ConfigureAwait(false);
                                }
                                finally
                                {
                                    releaser.Dispose();
                                }
                            }
                        }
                    }
                    finally
                    {
                        releaser?.Dispose();
                    }
                }
                else // ���������� ������ ������ �������.
                {
                    return WaitLockAsync(lockTask.AsTask(), asyncAction);

                    static async Task WaitLockAsync(Task<LockReleaser> task, Func<Task> asyncAction)
                    {
                        IDisposable releaser = await task.ConfigureAwait(false);
                        try
                        {
                            await asyncAction().ConfigureAwait(false);
                        }
                        finally
                        {
                            releaser.Dispose();
                        }
                    }
                }
            }
            else
                throw new ArgumentNullException(nameof(asyncAction));
        }

        /// <summary>
        /// Action ����� ��������� ��������� ��������� � �������� (Task) ���������� ����� LockAsync � ����� ���������� ��������������� 
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public Task LockAsync(Action action)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (action != null)
            {
                var task = LockAsync();
                if (task.IsCompletedSuccessfully)
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        task.Result.Dispose();
                    }

                    return Task.CompletedTask;
                }
                else
                {
                    return WaitAsync(task.AsTask(), action);

                    static async Task WaitAsync(Task<LockReleaser> task, Action action)
                    {
                        IDisposable releaser = await task.ConfigureAwait(false);
                        try
                        {
                            action();
                        }
                        finally
                        {
                            releaser.Dispose();
                        }
                    }
                }
            }
            else
                throw new ArgumentNullException(nameof(action));
        }

        /// <summary>
        /// ��������� ���������� �� ��� ��� ���� �� ����� ��������� ����������
        /// ��������������� ������������ ������ � �������� ���������� <see cref="AsyncLock"/>.
        /// ������������ ���������� ������������ ������� <see cref="LockReleaser.Dispose"/>.
        /// </summary>
        /// <returns>������ ������������ ����������.</returns>
        public ValueTask<LockReleaser> LockAsync()
        {
            // ������� ��������� ���������� ��������.
            bool taken = Interlocked.CompareExchange(ref _taken, 1, 0) == 0;

            if (taken) // ��������� ����������.
            {
                // �������� �� �� ��� �� �� ��������� _syncObj,
                // ������ ������ �� ����� ������� CreateNextReleaser ������������ � ����.

                LockReleaser releaser = CreateNextReleaser();

                return new ValueTask<LockReleaser>(result: releaser);
            }
            else
            {
                lock (_syncObj)
                {
                    if (_taken == 1) // ���������� ������ ������ ������� -> ���������� � �������.
                    {
                        return new ValueTask<LockReleaser>(task: _queue.EnqueueAndWait());
                    }
                    else
                    {
                        _taken = 1;

                        var releaser = SafeCreateNextReleaser();

                        return new ValueTask<LockReleaser>(result: releaser);
                    }
                }
            }
        }

        /// <summary>
        /// ����������� ���������� �� ������� ������������.
        /// </summary>
        internal void ReleaseLock(LockReleaser userReleaser)
        {
            Debug.Assert(_taken == 1, "��������� ������� ������� ����������");
            Debug.Assert(userReleaser.ReleaseToken == _releaseTaskToken, "������������ ���������� ����� �������");

            lock (_syncObj)
            {
                if (userReleaser.ReleaseToken == _releaseTaskToken) // � �������� ������ (��������) ���� ����� ���������� ����������.
                {
                    if (_queue.Count == 0) // ������ ������� ��� -> ���������� ����������.
                    {
                        // ��������� ����������� ���������� ���� �������.
                        SafeGetNextReleaserToken();

                        _taken = 0;
                    }
                    else // �� ���������� ���������� ������ ������.
                    {
                        // �������� �������� ����������� ���������� ������ (��������� ����� � ����������� ������).
                        _queue.DequeueAndEnter(SafeCreateNextReleaser());
                    }
                }
            }
        }

        /// <summary>
        /// ����������� ������������� ���-�� �������������� ��� ����� ��������� <see cref="LockReleaser"/>.
        /// </summary>
        /// <remarks>����������� <see cref="_releaseTaskToken"/>.</remarks>
        /// <returns><see cref="LockReleaser"/> � �������� ���� ������������ ����� ���������� ������� ����������.</returns>
        private LockReleaser CreateNextReleaser()
        {
            Debug.Assert(_taken == 1, "���������� ������ ���� ���������");

            return new LockReleaser(this, GetNextReleaserToken());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LockReleaser SafeCreateNextReleaser()
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));
            Debug.Assert(_taken == 1, "���������� ������ ���� ���������");

            return new LockReleaser(this, SafeGetNextReleaserToken());
        }

        /// <summary>
        /// ������������� ������������ ���������� ����� �������.
        /// </summary>
        /// <remarks>����������� <see cref="_releaseTaskToken"/>.</remarks>
        private short GetNextReleaserToken()
        {
            Debug.Assert(_taken == 1);
            return ++_releaseTaskToken;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short SafeGetNextReleaserToken()
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));
            return GetNextReleaserToken();
        }

        private sealed class WaitQueue
        {
            private readonly AsyncLock _context;

            /// <summary>
            /// ������� ��������� ������� (������) ������������ �� ������ ����������.
            /// </summary>
            /// <remarks>������ ������ ����� ���������� <see cref="_context._syncObj"/>.</remarks>
            private readonly Queue<TaskCompletionSource<LockReleaser>> _queue = new Queue<TaskCompletionSource<LockReleaser>>();

            public int Count => _queue.Count;

            public WaitQueue(AsyncLock context)
            {
                _context = context;
            }

            /// <summary>
            /// ��������� ����� � ������� �� �������� ������������ ����������.
            /// </summary>
            internal Task<LockReleaser> EnqueueAndWait()
            {
                Debug.Assert(Monitor.IsEntered(_context._syncObj), "��������� ����� ������ � ����������");

                var tcs = new TaskCompletionSource<LockReleaser>(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Enqueue(tcs); // �������� � �����.
                return tcs.Task;
            }

            internal void DequeueAndEnter(LockReleaser releaser)
            {
                Debug.Assert(Monitor.IsEntered(_context._syncObj), "��������� ����� ������ � ����������");
                Debug.Assert(_queue.Count > 0);

                // ����� ������ ����� � �������.
                var tcs = _queue.Dequeue();

                bool success = tcs.TrySetResult(releaser);
                Debug.Assert(success);
            }
        }

        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly AsyncLock _self;

            public DebugView(AsyncLock self)
            {
                _self = self;
            }

            /// <summary>
            /// ������� ������� (������) ������� ����������.
            /// </summary>
            public int PendingTasks => _self._queue.Count;
        }
    }
}