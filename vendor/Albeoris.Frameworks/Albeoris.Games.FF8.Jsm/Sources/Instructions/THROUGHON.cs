using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class THROUGHON : JsmInstruction
    {
        public THROUGHON()
        {
        }

        public THROUGHON(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(THROUGHON)}()";
        }
    }
}