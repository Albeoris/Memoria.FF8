using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class LASTOUT : JsmInstruction
    {
        public LASTOUT()
        {
        }

        public LASTOUT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(LASTOUT)}()";
        }
    }
}