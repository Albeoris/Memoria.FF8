using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class RESETGF : JsmInstruction
    {
        private IJsmExpression _arg0;

        public RESETGF(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public RESETGF(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(RESETGF)}({nameof(_arg0)}: {_arg0})";
        }
    }
}