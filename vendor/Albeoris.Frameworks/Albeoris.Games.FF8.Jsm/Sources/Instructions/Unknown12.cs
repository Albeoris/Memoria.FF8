using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class Unknown12 : JsmInstruction
    {
        public Unknown12()
        {
        }

        public Unknown12(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(Unknown12)}()";
        }
    }
}