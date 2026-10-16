using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IAwaitable
    {
        IAwaiter GetAwaiter();
    }
}