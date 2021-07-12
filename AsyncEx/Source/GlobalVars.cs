using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    internal static class GlobalVars
    {
        public static readonly Task<bool> CompletedTrueTask = Task.FromResult(true);
        public static readonly Task<bool> CompletedFalseTask = Task.FromResult(false);
    }
}
