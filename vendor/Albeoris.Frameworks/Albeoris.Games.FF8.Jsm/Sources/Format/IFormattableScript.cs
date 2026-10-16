using Albeoris.Games.FF8.Jsm.Core;

namespace Albeoris.Games.FF8.Jsm.Format
{
    public interface IFormattableScript
    {
        void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services);
    }
}