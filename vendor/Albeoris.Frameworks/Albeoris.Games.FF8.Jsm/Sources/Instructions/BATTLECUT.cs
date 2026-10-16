using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class BATTLECUT : JsmInstruction
    {
        public BATTLECUT()
        {
        }

        public BATTLECUT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(BATTLECUT)}()";
        }
    }
}