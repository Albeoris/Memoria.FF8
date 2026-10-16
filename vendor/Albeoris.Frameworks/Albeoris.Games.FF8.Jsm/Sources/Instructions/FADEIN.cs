using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FADEIN : JsmInstruction
    {
        public FADEIN()
        {
        }

        public FADEIN(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FADEIN)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IFieldService))
                .Method(nameof(IFieldService.FadeIn))
                .Comment(nameof(FADEIN));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Field[services].FadeIn();
            return DummyAwaitable.Instance;
        }
    }
}