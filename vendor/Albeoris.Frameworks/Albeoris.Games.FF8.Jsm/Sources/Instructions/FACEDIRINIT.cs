using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FACEDIRINIT : JsmInstruction
    {
        public FACEDIRINIT()
        {
        }

        public FACEDIRINIT(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FACEDIRINIT)}()";
        }
    }
}