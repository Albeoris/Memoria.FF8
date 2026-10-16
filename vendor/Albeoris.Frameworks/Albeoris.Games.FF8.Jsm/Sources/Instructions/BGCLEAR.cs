using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class BGCLEAR : JsmInstruction
    {
        private IJsmExpression _arg0;

        public BGCLEAR(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public BGCLEAR(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(BGCLEAR)}({nameof(_arg0)}: {_arg0})";
        }
    }
}