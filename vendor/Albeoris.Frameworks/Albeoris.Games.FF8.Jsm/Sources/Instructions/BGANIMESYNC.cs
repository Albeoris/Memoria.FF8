using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class BGANIMESYNC : JsmInstruction
    {
        public BGANIMESYNC()
        {
        }

        public BGANIMESYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(BGANIMESYNC)}()";
        }
    }
}