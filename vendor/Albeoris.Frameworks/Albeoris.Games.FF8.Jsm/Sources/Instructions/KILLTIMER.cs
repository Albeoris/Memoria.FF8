using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class KILLTIMER : JsmInstruction
    {
        public KILLTIMER()
        {
        }

        public KILLTIMER(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(KILLTIMER)}()";
        }
    }
}