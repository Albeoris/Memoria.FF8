using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MUSICCHANGE : JsmInstruction
    {
        public MUSICCHANGE()
        {
        }

        public MUSICCHANGE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MUSICCHANGE)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IMusicService))
                .Method(nameof(IMusicService.PlayFieldMusic))
                .Comment(nameof(MUSICCHANGE));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Music[services].PlayFieldMusic();
            return DummyAwaitable.Instance;
        }
    }
}