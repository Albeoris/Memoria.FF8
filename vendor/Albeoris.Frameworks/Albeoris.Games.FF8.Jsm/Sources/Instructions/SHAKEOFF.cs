using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SHAKEOFF : JsmInstruction
    {
        public SHAKEOFF()
        {
        }

        public SHAKEOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(SHAKEOFF)}()";
        }
    }
}