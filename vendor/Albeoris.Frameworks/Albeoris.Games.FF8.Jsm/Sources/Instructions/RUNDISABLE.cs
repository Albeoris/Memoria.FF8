using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class RUNDISABLE : JsmInstruction
    {
        public RUNDISABLE()
        {
        }

        public RUNDISABLE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(RUNDISABLE)}()";
        }
    }
}