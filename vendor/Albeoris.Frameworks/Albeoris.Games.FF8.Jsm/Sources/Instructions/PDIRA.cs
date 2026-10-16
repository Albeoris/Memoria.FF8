using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class PDIRA : JsmInstruction
    {
        private IJsmExpression _arg0;

        public PDIRA(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public PDIRA(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(PDIRA)}({nameof(_arg0)}: {_arg0})";
        }
    }
}