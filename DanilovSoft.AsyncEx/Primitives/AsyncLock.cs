﻿using System.Collections.Generic;
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

        // Для добавления потока в очередь и удаления из очереди.
        private readonly object _syncObj = new();

        /// <summary>
        /// Очередь пользовательских тасков, которые хотят получить блокировку.
        /// </summary>
        /// <remarks>Доступ через блокировку <see cref="_syncObj"/></remarks>
        private readonly WaitQueue _queue;

        /// <summary>
        /// Токен для потока у которого есть право освободить блокировку.
        /// Может только увеличиваться.
        /// </summary>
        /// <remarks>Превентивная защита от освобождения блокировки чужим потоком.</remarks>
        internal short ReleaseTaskToken;

        /// <summary>
        /// Когда блокировка захвачена таском.
        /// </summary>
        /// <remarks>Модификация через блокировку <see cref="_syncObj"/> или атомарно.</remarks>
        private int _taken;

        public AsyncLock()
        {
            _queue = new WaitQueue(this);
        }

        /// <summary>
        /// Блокирует выполнение до тех пор пока не будет захвачена блокировка
        /// предоставляющая эксклюзивный доступ к текущему экземпляру <see cref="AsyncLock"/>.
        /// Освобождение блокировки производится вызовом <see cref="LockReleaser.Dispose"/>.
        /// </summary>
        /// <returns>Ресурс удерживающий блокировку.</returns>
        public ValueTask<LockReleaser> LockAsync()
        {
            // Попытка захватить блокировку атомарно (Fast-Path).
            bool taken = Interlocked.CompareExchange(ref _taken, 1, 0) == 0;
            if (taken)
            {
                // Несмотря на то что мы НЕ захватили _syncObj,
                // другие потоки не могут вызвать CreateNextReleaser одновременно с нами.
                var releaser = CreateNextReleaser();
                return ValueTask.FromResult(releaser);
            }
            else
            {
                lock (_syncObj) // Slow-Path.
                {
                    if (_taken == 1) // Блокировка занята другим потоком -> становимся в очередь.
                    {
                        return new(task: _queue.EnqueueAndWait());
                    }
                    else
                    {
                        _taken = 1;
                        var releaser = SafeCreateNextReleaser();
                        return ValueTask.FromResult(releaser);
                    }
                }
            }
        }

        /// <summary>
        /// Освобождает блокировку по запросу пользователя.
        /// </summary>
        internal void ReleaseLock(LockReleaser userReleaser)
        {
            Debug.Assert(_taken == 1, "Нарушение порядка захвата блокировки");
            Debug.Assert(userReleaser.ReleaseToken == ReleaseTaskToken, "Освобождение блокировки чужим потоком");

            lock (_syncObj)
            {
                if (userReleaser.ReleaseToken == ReleaseTaskToken) // У текущего потока (релизера) есть право освободить блокировку.
                {
                    if (_queue.Count == 0) // Больше потоков нет -> освободить блокировку.
                    {
                        // Запретить освобождать блокировку всем потокам.
                        SafeGetNextReleaserToken();

                        _taken = 0;
                    }
                    else // На блокировку претендуют другие потоки.
                    {
                        // Передать владение блокировкой следующему потоку (разрешить войти в критическую секцию).
                        _queue.DequeueAndEnter(SafeCreateNextReleaser());
                    }
                }
            }
        }

        /// <summary>
        /// Увеличивает идентификатор что-бы инвалидировать все ранее созданные <see cref="LockReleaser"/>.
        /// </summary>
        /// <remarks>Увеличивает <see cref="ReleaseTaskToken"/>.</remarks>
        /// <returns><see cref="LockReleaser"/> у которого есть эксклюзивное право освободить текущую блокировку.</returns>
        private LockReleaser CreateNextReleaser()
        {
            Debug.Assert(_taken == 1, "Блокировка должна быть захвачена");

            return new LockReleaser(this, GetNextReleaserToken());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LockReleaser SafeCreateNextReleaser()
        {
            Debug.Assert(Monitor.IsEntered(_syncObj));
            Debug.Assert(_taken == 1, "Блокировка должна быть захвачена");

            return new LockReleaser(this, SafeGetNextReleaserToken());
        }

        /// <summary>
        /// Предотвращает освобождение блокировки чужим потоком.
        /// </summary>
        /// <remarks>Увеличивает <see cref="ReleaseTaskToken"/>.</remarks>
        private short GetNextReleaserToken()
        {
            Debug.Assert(_taken == 1);
            return ++ReleaseTaskToken;
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
            /// Очередь ожидающий потоков (тасков) претендующих на захват блокировки.
            /// </summary>
            /// <remarks>Доступ только через блокировку <see cref="_syncObj"/>.</remarks>
            private readonly Queue<TaskCompletionSource<LockReleaser>> _queue = new();

            public int Count => _queue.Count;

            public WaitQueue(AsyncLock context)
            {
                _context = context;
            }

            /// <summary>
            /// Добавляет поток в очередь на ожидание эксклюзивной блокировки.
            /// </summary>
            internal Task<LockReleaser> EnqueueAndWait()
            {
                Debug.Assert(Monitor.IsEntered(_context._syncObj), "Выполнять можно только в блокировке");

                var tcs = new TaskCompletionSource<LockReleaser>(TaskCreationOptions.RunContinuationsAsynchronously);
                _queue.Enqueue(tcs); // Добавить в конец.
                return tcs.Task;
            }

            internal void DequeueAndEnter(LockReleaser releaser)
            {
                Debug.Assert(Monitor.IsEntered(_context._syncObj), "Выполнять можно только в блокировке");
                Debug.Assert(_queue.Count > 0);

                // Взять первый поток в очереди.
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
            /// Сколько потоков (тасков) ожидают блокировку.
            /// </summary>
            public int PendingTasks => _self._queue.Count;
        }
    }
}