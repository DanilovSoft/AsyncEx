using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks.Sources;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            var deb = new Debounce<string>(s => _ = s.Length, 10_000);

            deb.Invoke("OK1");
            Thread.Sleep(10_100);
            deb.Invoke("OK2");
            Thread.Sleep(-1);
        }
    }
}
