using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MENUENABLE : JsmInstruction
    {
        public MENUENABLE()
        {
        }

        public MENUENABLE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MENUENABLE)}()";
        }
    }
}