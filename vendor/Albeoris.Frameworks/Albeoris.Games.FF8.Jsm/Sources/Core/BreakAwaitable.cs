namespace Albeoris.Games.FF8.Jsm.Core
{
    public sealed class BreakAwaitable : IAwaitable
    {
        public static IAwaitable Instance { get; } = new BreakAwaitable();

        public IAwaiter GetAwaiter()
        {
            return DummyAwaiter.Instance;
        }
    }
}