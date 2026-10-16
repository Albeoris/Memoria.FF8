using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SWAP : JsmInstruction
    {
        public SWAP()
        {
        }

        public SWAP(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(SWAP)}()";
        }
    }
}