using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;

namespace DanilovSoft.AsyncEx
{
    [DebuggerDisplay(@"\{State = {DebugDisplay,nq}\}")]
    [DebuggerTypeProxy(typeof(ManualResetEventSource<>.DebugView))]
    public sealed class ManualResetEventSource<T>
    {
        private const string ConsistencyError = "Can't Take before Reset";
        private string DebugDisplay => _state.ToString();
        private readonly object _syncObj = new object();
        private volatile State _state = State.NoItem;
        [MaybeNull, AllowNull]
        private T _item = default;

        public ManualResetEventSource()
        {

        }

        public void Reset()
        {
            lock (_syncObj)
            {
                _item = default;
                _state = State.WaitingItem;
            }
        }

        public bool Reset([MaybeNull] out T item)
        {
            lock (_syncObj)
            {
                bool ret;
                if (_state == State.HoldsItem)
                {
                    item = _item;
                    ret = true;
                }
                else
                {
                    item = default;
                    ret = false;
                }

                _item = default;
                _state = State.WaitingItem;
                return ret;
            }
        }

        public bool TryTake([MaybeNull] out T item)
        {
            lock (_syncObj)
            {
                if (_state == State.HoldsItem)
                // Поток поставщик первее зашел в критическую секцию и уже сохранил объект.
                {
                    item = _item;
                    _item = default;
                    _state = State.NoItem;
                    return true;
                }
                else if (_state == State.WaitingItem)
                {
                    item = default;
                    return false;
                }
                else
                // NoItem
                {
                    throw new InvalidOperationException(ConsistencyError);
                }
            }
        }

        public bool TrySet(T item)
        {
            // Fast-Path проверка.
            if (_state == State.WaitingItem)
            {
                lock (_syncObj)
                {
                    if (_state == State.WaitingItem)
                    {
                        _item = item;

                        // Объект успешно сохранён для потока потребителя.
                        _state = State.HoldsItem;

                        // Если есть ожидающий поток потребителя, то разблокировать его.
                        Monitor.Pulse(_syncObj);

                        return true;
                    }
                }
            }
            return false;
        }

        /// <exception cref="InvalidOperationException"/>
        public bool Wait(TimeSpan timeout, [MaybeNullWhen(false)] out T item)
        {
            lock (_syncObj)
            {
                if (_state == State.WaitingItem)
                // Мы первее зашли в критическую секцию, чем поток поставщик.
                {
                    // Явно разрешаем поставщику зайти в критическую секцию.
                    if (Monitor.Wait(_syncObj, timeout: timeout))
                    {
                        item = _item;
                        _item = default;
                        _state = State.NoItem;
                        return true;
                    }
                    else
                    // Поставщик не зашёл в критическую секцию за предоставленное время.
                    {
                        item = default;
                        return false;
                    }
                }
                else if (_state == State.HoldsItem)
                // Поток поставщик первее зашел в критическую секцию и уже сохранил объект.
                {
                    item = _item;
                    _item = default;
                    _state = State.NoItem;
                    return true;
                }
                else
                // NoItem
                {
                    throw new InvalidOperationException(ConsistencyError);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"/>
        [return: MaybeNull]
        public T Wait()
        {
            Wait(Timeout.InfiniteTimeSpan, out T item);
            return item;
        }

        private enum State
        {
            NoItem,
            WaitingItem,
            HoldsItem
        }

        /// <summary>A debugger view of the <see cref="LazyAsync{T}"/> to surface additional debugging properties and 
        /// to ensure that the <see cref="LazyAsync{T}"/> does not become initialized if it was not already.
        /// </summary>
        private sealed class DebugView
        {
            private readonly ManualResetEventSource<T> _self;

            public DebugView(ManualResetEventSource<T> self)
            {
                _self = self;
            }

            [MaybeNull]
            public T Item => _self._item;
            public State State => _self._state;
        }
    }
}
