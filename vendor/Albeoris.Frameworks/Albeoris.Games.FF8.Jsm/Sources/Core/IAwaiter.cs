using System;
using System.Runtime.CompilerServices;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IAwaiter : INotifyCompletion
    {
        Boolean IsCompleted { get; }
        void GetResult();
    }
}