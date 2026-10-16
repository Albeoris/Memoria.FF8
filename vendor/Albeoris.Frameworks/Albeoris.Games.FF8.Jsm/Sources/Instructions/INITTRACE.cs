using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class INITTRACE : JsmInstruction
    {
        public INITTRACE()
        {
        }

        public INITTRACE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(INITTRACE)}()";
        }
    }
}