using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SHADELEVEL : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SHADELEVEL(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SHADELEVEL(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SHADELEVEL)}({nameof(_arg0)}: {_arg0})";
        }
    }
}