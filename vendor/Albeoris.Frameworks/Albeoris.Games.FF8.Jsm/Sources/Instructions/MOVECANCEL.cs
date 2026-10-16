using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MOVECANCEL : JsmInstruction
    {
        private IJsmExpression _arg0;

        public MOVECANCEL(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public MOVECANCEL(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MOVECANCEL)}({nameof(_arg0)}: {_arg0})";
        }
    }
}