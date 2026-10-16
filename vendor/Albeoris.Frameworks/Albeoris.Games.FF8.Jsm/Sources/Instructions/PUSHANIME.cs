using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class PUSHANIME : JsmInstruction
    {
        public PUSHANIME()
        {
        }

        public PUSHANIME(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(PUSHANIME)}()";
        }
    }
}