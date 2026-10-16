using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FOOTSTEPCUT : JsmInstruction
    {
        public FOOTSTEPCUT()
        {
        }

        public FOOTSTEPCUT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FOOTSTEPCUT)}()";
        }
    }
}