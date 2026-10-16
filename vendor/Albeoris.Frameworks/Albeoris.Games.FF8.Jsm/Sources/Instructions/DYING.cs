using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class DYING : JsmInstruction
    {
        public DYING()
        {
        }

        public DYING(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(DYING)}()";
        }
    }
}