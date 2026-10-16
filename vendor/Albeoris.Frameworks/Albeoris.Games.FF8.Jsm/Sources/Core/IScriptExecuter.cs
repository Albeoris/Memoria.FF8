using System.Collections.Generic;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IScriptExecuter
    {
        IEnumerable<IAwaitable> Execute(IServices services);
    }
}