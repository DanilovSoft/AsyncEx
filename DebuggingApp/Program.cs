using System.Threading;
using System.Threading.Tasks;

using DanilovSoft.AsyncEx;

namespace TestApp;

class Program
{
    static async Task Main()
    {
        var lazy = new AsyncLazy<int>(async _ => { return 1; });
        var v = await lazy.GetValueAsync();


    }
}
