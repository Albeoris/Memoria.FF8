using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class LINEON : JsmInstruction
    {
        public LINEON()
        {
        }

        public LINEON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(LINEON)}()";
        }
    }
}