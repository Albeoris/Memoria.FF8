using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FOLLOWON : JsmInstruction
    {
        public FOLLOWON()
        {
        }

        public FOLLOWON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FOLLOWON)}()";
        }
    }
}