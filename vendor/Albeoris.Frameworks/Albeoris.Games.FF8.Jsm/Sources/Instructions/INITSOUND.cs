using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class INITSOUND : JsmInstruction
    {
        public INITSOUND()
        {
        }

        public INITSOUND(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(INITSOUND)}()";
        }
    }
}