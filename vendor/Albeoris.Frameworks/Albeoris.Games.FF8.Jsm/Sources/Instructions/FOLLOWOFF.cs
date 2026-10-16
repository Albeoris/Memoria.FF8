using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FOLLOWOFF : JsmInstruction
    {
        public FOLLOWOFF()
        {
        }

        public FOLLOWOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FOLLOWOFF)}()";
        }
    }
}