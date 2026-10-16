using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MOVIECUT : JsmInstruction
    {
        public MOVIECUT()
        {
        }

        public MOVIECUT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MOVIECUT)}()";
        }
    }
}