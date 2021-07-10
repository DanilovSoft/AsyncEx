using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public static class Debounce
    {
        public static Debounce<T> Create<T>(Action<T> callback, TimeSpan delay)
        {
            return new Debounce<T>(callback, delay);
        }
    }

    public sealed class Debounce<T>
    {
        private readonly object _syncObj = new();
        private readonly Action<T> _callback;
        private readonly TimeSpan _delay;
        private Action<T>? _debounce;
        private Timer? _timer;

        public Debounce(Action<T> callback, int delayMsec) : this(callback, TimeSpan.FromMilliseconds(delayMsec))
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
            _debounce = null;
            _timer = null;
        }

        public void Invoke(T arg)
        {
            lock (_syncObj)
            {
                _timer = new Timer(OnTimer, this, _delay, Timeout.InfiniteTimeSpan);
            }
        }

        public void Cancel()
        {
            
        }

        private void OnTimer(object? state)
        {

        }
    }
}
