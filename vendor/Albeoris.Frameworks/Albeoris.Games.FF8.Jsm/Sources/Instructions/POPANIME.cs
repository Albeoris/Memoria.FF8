using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class POPANIME : JsmInstruction
    {
        public POPANIME()
        {
        }

        public POPANIME(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(POPANIME)}()";
        }
    }
}