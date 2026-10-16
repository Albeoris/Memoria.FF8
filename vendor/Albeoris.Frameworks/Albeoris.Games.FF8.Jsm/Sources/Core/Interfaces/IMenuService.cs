using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IMenuService
    {
        Boolean IsSupported { get; }

        IAwaitable ShowEnterNameDialog(NamedEntity entity);
    }
}