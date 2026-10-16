using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FADEBLACK : JsmInstruction
    {
        public FADEBLACK()
        {
        }

        public FADEBLACK(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FADEBLACK)}()";
        }
    }
}