using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FADEOUT : JsmInstruction
    {
        public FADEOUT()
        {
        }

        public FADEOUT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FADEOUT)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IFieldService))
                .Method(nameof(IFieldService.FadeOut))
                .Comment(nameof(FADEOUT));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Field[services].FadeOut();
            return DummyAwaitable.Instance;
        }
    }
}