using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class ANIMESTOP : JsmInstruction
    {
        public ANIMESTOP()
        {
        }

        public ANIMESTOP(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(ANIMESTOP)}()";
        }
    }
}