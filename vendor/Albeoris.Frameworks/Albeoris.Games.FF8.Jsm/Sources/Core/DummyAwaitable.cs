using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public sealed class DummyAwaitable : IAwaitable
    {
        public static IAwaitable Instance { get; } = new DummyAwaitable();

        public IAwaiter GetAwaiter()
        {
            return DummyAwaiter.Instance;
        }
    }
}