using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class CLOSEEYES : JsmInstruction
    {
        public CLOSEEYES()
        {
        }

        public CLOSEEYES(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(CLOSEEYES)}()";
        }
    }
}