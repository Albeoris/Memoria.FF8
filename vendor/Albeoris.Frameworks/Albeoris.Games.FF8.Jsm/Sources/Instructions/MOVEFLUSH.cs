using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MOVEFLUSH : JsmInstruction
    {
        public MOVEFLUSH()
        {
        }

        public MOVEFLUSH(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MOVEFLUSH)}()";
        }
    }
}