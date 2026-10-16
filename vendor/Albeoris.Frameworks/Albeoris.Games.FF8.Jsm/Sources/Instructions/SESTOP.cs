using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SESTOP : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SESTOP(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SESTOP(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SESTOP)}({nameof(_arg0)}: {_arg0})";
        }
    }
}