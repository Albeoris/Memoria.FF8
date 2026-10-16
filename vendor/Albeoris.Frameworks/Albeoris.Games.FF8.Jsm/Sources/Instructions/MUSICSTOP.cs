using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MUSICSTOP : JsmInstruction
    {
        private IJsmExpression _arg0;

        public MUSICSTOP(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public MUSICSTOP(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MUSICSTOP)}({nameof(_arg0)}: {_arg0})";
        }
    }
}