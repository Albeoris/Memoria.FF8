using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class GETTIMER : JsmInstruction
    {
        public GETTIMER()
        {
        }

        public GETTIMER(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(GETTIMER)}()";
        }
    }
}