using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class THROUGHOFF : JsmInstruction
    {
        public THROUGHOFF()
        {
        }

        public THROUGHOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(THROUGHOFF)}()";
        }
    }
}