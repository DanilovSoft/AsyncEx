using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public sealed class Debounce<T> : IDisposable
    {
        private readonly object _invokeObj = new();
        private readonly object _timerObj = new();
        private readonly TimeSpan _delay;
        private Action<T>? _callback;
        private Timer? _timer;
        /// <summary>
        /// Чтение и запись только в блокировке _invokeObj.
        /// </summary>
        [AllowNull] private T _arg;
        /// <summary>
        /// Чтение и запись только в блокировке _invokeObj.
        /// </summary>
        private bool _cancelState;
        private bool _disposed;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delay">Задержка в миллисекундах.</param>
        public Debounce(Action<T> callback, int delay) : this(callback, TimeSpan.FromMilliseconds(delay))
        {
        }

        public Debounce(Action<T> callback, TimeSpan delay)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));

            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            _delay = delay;
            _timer = new Timer(static s => ((Debounce<T>)s!).OnTimer(), this, -1, -1);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
                _callback = null;
                _arg = default;
            }
        }

        /// <exception cref="ObjectDisposedException"/>
        public void Invoke(T arg)
        {
            CheckDisposed();

            lock (_invokeObj)
            {
                _arg = arg;             // 1) Сначала установить аргумент.
                _cancelState = false;   // 2) Затем снять флаг.

                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Не вызывать одновременно с Invoke.
        /// </summary>
        /// <exception cref="ObjectDisposedException"/>
        public void Cancel(bool waitLastCallback = true)
        {
            CheckDisposed();

            lock (_invokeObj)
            {
                _cancelState = true;    // 1) Сначала поднять флаг.
                _arg = default;         // 2) Затем занулить аргумент.

                // Пытаемся отменить тик таймер.
                _timer.Change(-1, -1);
                // Помним что таймер всё ещё может сработать.

                if (waitLastCallback)
                {
                    if (Monitor.TryEnter(_timerObj))
                    {
                        // Успешно захватили лок, значит последующий тик гарантированно увидит флаг и будет no-op.

                        Monitor.Exit(_timerObj);
                        return;
                    }
                }
            }

            if (waitLastCallback)
            {
                // Если дошли сюда, значит таймер находился в процессе выполнения тика.
                // Нам нужно дождаться завершения именно ЭТОГО тика, для этого будет достаточно факта захвата блокировки.

                // Безобитный нюанс: Если пользователь вопреки документации, одновременно с отменой вызовет Invoke,
                // тогда есть вероятность что мы встанем на ожидание уже СЛЕДУЮЩЕГО тика таймера.

                // Захватить и отпустить лок таймера.
                bool lockTaken = false;
                try
                {
                    Monitor.Enter(_timerObj, ref lockTaken);
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(_timerObj);
                    }
                }
            }
        }

        private void OnTimer()
        {
            lock (_timerObj)
            {
                T arg;
                lock (_invokeObj)
                {
                    if (!_cancelState)
                    {
                        arg = _arg;
                        _arg = default;
                    }
                    else
                    {
                        return;
                    }
                }

                _callback?.Invoke(arg);
            }
        }

        /// <exception cref="ObjectDisposedException"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MemberNotNull(nameof(_timer), nameof(_callback))]
        private void CheckDisposed()
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposed<Debounce<T>>();
            }

            Debug.Assert(_timer != null);
            Debug.Assert(_callback != null);
        }
    }
}
