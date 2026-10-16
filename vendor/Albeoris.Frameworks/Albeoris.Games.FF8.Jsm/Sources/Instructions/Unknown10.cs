using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class Unknown10 : JsmInstruction
    {
        public Unknown10()
        {
        }

        public Unknown10(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(Unknown10)}()";
        }
    }
}