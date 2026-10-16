using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class REST : JsmInstruction
    {
        public REST()
        {
        }

        public REST(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(REST)}()";
        }
    }
}