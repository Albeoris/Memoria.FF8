using System;
using Albeoris.Games.FF8.Jsm.Core;

namespace Albeoris.Games.FF8.Jsm.Format
{
    public sealed class StatelessServices : IServices
    {
        public static IServices Instance { get; } = new StatelessServices();

        private StatelessServices()
        {
        }

        public T Service<T>(ServiceId<T> id)
        {
            return (T)(Object)id;
        }
    }
}