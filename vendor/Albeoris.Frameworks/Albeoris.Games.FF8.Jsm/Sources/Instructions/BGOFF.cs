using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class BGOFF : JsmInstruction
    {
        public BGOFF()
        {
        }

        public BGOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(BGOFF)}()";
        }
    }
}