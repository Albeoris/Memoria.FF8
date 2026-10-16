using System;
using Albeoris.Games.FF8.Jsm.Core;
using Albeoris.Games.FF8.Jsm.Format;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class ANIMESYNC : JsmInstruction
    {
        public ANIMESYNC()
        {
        }

        public ANIMESYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(ANIMESYNC)}()";
        }

        public override void Format(ScriptWriter sw, IScriptFormatterContext formatterContext, IServices services)
        {
            sw.Format(formatterContext, services)
                .Await()
                .Property(nameof(FieldObject.Animation))
                .Method(nameof(FieldObjectAnimation.Wait))
                .Comment(nameof(ANIMESYNC));
        }

        public override IAwaitable TestExecute(IServices services)
        {
            return ServiceId.Field[services].Engine.CurrentObject.Animation.Wait();
        }
    }
}