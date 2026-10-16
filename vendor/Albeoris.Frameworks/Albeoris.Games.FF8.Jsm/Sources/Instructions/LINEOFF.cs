using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class LINEOFF : JsmInstruction
    {
        public LINEOFF()
        {
        }

        public LINEOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(LINEOFF)}()";
        }
    }
}