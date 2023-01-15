using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    /// <summary>
    /// Позволяет по запросу обмениваться объектами между потоками.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [DebuggerDisplay(@"\{State = {DebugDisplay,nq}\}")]
    [DebuggerTypeProxy(typeof(ManualResetEventSource<>.DebugView))]
    public sealed class ManualResetEventSource<T>
    {
        private const string NotReadyError = "Can't Take before Reset";
        private readonly object _syncObj = new();
        private volatile State _state = State.WaitingItem;
        [AllowNull]
        private T _item = default;

        public ManualResetEventSource()
        {
            
        }

        private string DebugDisplay => _state.ToString();

        public void Reset()
        {
            lock (_syncObj)
            {
                _item = default;
                _state = State.WaitingItem;
            }
        }

        public bool Reset([MaybeNullWhen(false)] out T item)
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

        /// <exception cref="InvalidOperationException"/>
        public bool TryTake([MaybeNullWhen(false)] out T item)
        {
            lock (_syncObj)
            {
                switch (_state)
                // Поток поставщик первее зашел в критическую секцию и уже сохранил объект.
                {
                    case State.HoldsItem:
                        item = _item;
                        _item = default;
                        _state = State.NoItem;
                        return true;
                    case State.WaitingItem:
                        item = default;
                        return false;
                    default:
                        throw new InvalidOperationException(NotReadyError);
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

        /// <returns>False если за отведённое время не получили объект от другого потока.</returns>
        /// <exception cref="InvalidOperationException"/>
        public bool Wait(TimeSpan timeout, [MaybeNullWhen(false)] out T item)
        {
            lock (_syncObj)
            {
                switch (_state)
                {
                    case State.WaitingItem:
                        // Мы первее зашли в критическую секцию, чем поток поставщик.
                        // Явно разрешаем поставщику зайти в критическую секцию.
                        if (Monitor.Wait(_syncObj, timeout, exitContext: false))
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
                    case State.HoldsItem:
                        item = _item;
                        _item = default;
                        _state = State.NoItem;
                        return true;
                    default: // NoItem
                        throw new InvalidOperationException(NotReadyError);
                }
            }
        }

        /// <exception cref="InvalidOperationException"/>
        public T Wait()
        {
            // С бесконечным таймаутом всегда возвращает True.
            Wait(Timeout.InfiniteTimeSpan, out var item);
            return item!;
        }

        private enum State
        {
            NoItem,
            WaitingItem,
            HoldsItem
        }

        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly ManualResetEventSource<T> _self;

            public DebugView(ManualResetEventSource<T> self)
            {
                _self = self;
            }

            public T Result => _self._item;
            public State State => _self._state;
        }
    }
}
