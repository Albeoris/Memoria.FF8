using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MAPFADEOFF : JsmInstruction
    {
        public MAPFADEOFF()
        {
        }

        public MAPFADEOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MAPFADEOFF)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IFieldService))
                .Method(nameof(IFieldService.FadeOff))
                .Comment(nameof(MAPFADEOFF));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Field[services].FadeOff();
            return DummyAwaitable.Instance;
        }
    }
}