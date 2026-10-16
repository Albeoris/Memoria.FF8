using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MOVESYNC : JsmInstruction
    {
        public MOVESYNC()
        {
        }

        public MOVESYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MOVESYNC)}()";
        }
    }
}