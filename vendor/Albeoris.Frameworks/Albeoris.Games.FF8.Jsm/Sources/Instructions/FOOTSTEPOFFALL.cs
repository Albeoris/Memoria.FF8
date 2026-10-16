using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FOOTSTEPOFFALL : JsmInstruction
    {
        public FOOTSTEPOFFALL()
        {
        }

        public FOOTSTEPOFFALL(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FOOTSTEPOFFALL)}()";
        }
    }
}