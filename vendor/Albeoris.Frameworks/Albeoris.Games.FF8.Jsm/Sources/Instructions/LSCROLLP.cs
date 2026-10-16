using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class LSCROLLP : JsmInstruction
    {
        private IJsmExpression _arg0;
        private IJsmExpression _arg1;

        public LSCROLLP(IJsmExpression arg0, IJsmExpression arg1)
        {
            _arg0 = arg0;
            _arg1 = arg1;
        }

        public LSCROLLP(Int32 parameter, IExpressionStack stack)
            : this(
                arg1: stack.Pop(),
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(LSCROLLP)}({nameof(_arg0)}: {_arg0}, {nameof(_arg1)}: {_arg1})";
        }
    }
}