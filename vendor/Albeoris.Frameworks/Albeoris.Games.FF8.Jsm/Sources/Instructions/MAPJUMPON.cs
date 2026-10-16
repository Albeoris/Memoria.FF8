using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MAPJUMPON : JsmInstruction
    {
        public MAPJUMPON()
        {
        }

        public MAPJUMPON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MAPJUMPON)}()";
        }
    }
}