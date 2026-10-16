using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class DOORLINEON : JsmInstruction
    {
        public DOORLINEON()
        {
        }

        public DOORLINEON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(DOORLINEON)}()";
        }
    }
}