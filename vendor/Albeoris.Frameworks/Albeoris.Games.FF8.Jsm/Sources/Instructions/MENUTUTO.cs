using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MENUTUTO : JsmInstruction
    {
        public MENUTUTO()
        {
        }

        public MENUTUTO(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MENUTUTO)}()";
        }
    }
}