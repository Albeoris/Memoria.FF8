using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MAPFADEON : JsmInstruction
    {
        public MAPFADEON()
        {
        }

        public MAPFADEON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MAPFADEON)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .StaticType(nameof(IFieldService))
                .Method(nameof(IFieldService.FadeOn))
                .Comment(nameof(MAPFADEON));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            ServiceId.Field[services].FadeOn();
            return DummyAwaitable.Instance;
        }
    }
}