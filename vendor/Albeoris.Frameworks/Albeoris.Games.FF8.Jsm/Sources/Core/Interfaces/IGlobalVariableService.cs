using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IGlobalVariableService
    {
        Boolean IsSupported { get; }

        T Get<T>(GlobalVariableId<T> id) where T : unmanaged;
        void Set<T>(GlobalVariableId<T> id, T value) where T : unmanaged;
    }
}