using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FOOTSTEPCOPY : JsmInstruction
    {
        public FOOTSTEPCOPY()
        {
        }

        public FOOTSTEPCOPY(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FOOTSTEPCOPY)}()";
        }
    }
}