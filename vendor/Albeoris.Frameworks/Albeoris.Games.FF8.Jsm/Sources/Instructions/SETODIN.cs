using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SETODIN : JsmInstruction
    {
        public SETODIN()
        {
        }

        public SETODIN(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(SETODIN)}()";
        }
    }
}