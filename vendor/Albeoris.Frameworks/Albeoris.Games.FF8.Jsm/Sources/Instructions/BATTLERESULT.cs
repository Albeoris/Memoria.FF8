using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class BATTLERESULT : JsmInstruction
    {
        public BATTLERESULT()
        {
        }

        public BATTLERESULT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(BATTLERESULT)}()";
        }
    }
}