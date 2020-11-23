using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static readonly ManualResetEventSource<string> _mcs1 = new ManualResetEventSource<string>();
        static readonly ManualResetEventSource<string> _mcs2 = new ManualResetEventSource<string>();

        static void Main()
        {
            new Thread(Thread1).Start();
            new Thread(Thread2).Start();
            MainThread();
        }

        static void MainThread()
        {
            while (true)
            {
                Thread.Sleep(3_000);

                _mcs1.Reset();
                _mcs2.Reset();

                if (_mcs1.Wait(timeout: TimeSpan.FromSeconds(5), out string? value1))
                {
                    // Передаёшь value1 в конвейер.
                }
                _mcs2.Wait(timeout: TimeSpan.FromSeconds(5), out string? value2);
            }
        }

        static void Thread1()
        {
            while (true)
            {
                Thread.Sleep(1_000);
                string value = new Random().Next().ToString();

                _mcs1.TrySet(value);
            }
        }

        static void Thread2()
        {
            while (true)
            {
                Thread.Sleep(1_000);
                string value = new Random().Next().ToString();

                _mcs2.TrySet(value);
            }
        }
    }
}
