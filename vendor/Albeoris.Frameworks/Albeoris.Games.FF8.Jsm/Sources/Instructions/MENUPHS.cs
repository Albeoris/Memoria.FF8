using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MENUPHS : JsmInstruction
    {
        public MENUPHS()
        {
        }

        public MENUPHS(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MENUPHS)}()";
        }
    }
}