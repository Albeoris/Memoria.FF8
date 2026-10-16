using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MENUDISABLE : JsmInstruction
    {
        public MENUDISABLE()
        {
        }

        public MENUDISABLE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MENUDISABLE)}()";
        }
    }
}