using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static readonly ManualResetEventSource<byte[]> _mcs1 = new ManualResetEventSource<byte[]>();

        static object _image;
        static ManualResetEventSlim _mreInit = new ManualResetEventSlim();
        static ManualResetEventSlim _mreFeedback = new ManualResetEventSlim();

        static void Main()
        {
            new Thread(CameraThread).Start();
            MainThread();
        }

        static void MainThread()
        {
            while (true)
            {
                Thread.Sleep(3_000);

                _mreFeedback.Reset();
                _mreInit.Set();

                _mreFeedback.Wait();

                object img = Volatile.Read(ref _image);
            }
        }

        static void CameraThread()
        {
            while (true)
            {
                Thread.Sleep(1_000);

                if (_mreInit.Wait(0))
                {
                    Volatile.Write(ref _image, new object());
                    _mreInit.Reset();
                    _mreFeedback.Set();
                }
            }
        }
    }
}
