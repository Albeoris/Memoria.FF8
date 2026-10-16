using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MUSICSTATUS : JsmInstruction
    {
        public MUSICSTATUS()
        {
        }

        public MUSICSTATUS(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MUSICSTATUS)}()";
        }
    }
}