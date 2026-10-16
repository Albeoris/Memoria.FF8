using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SETTIMER : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SETTIMER(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SETTIMER(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SETTIMER)}({nameof(_arg0)}: {_arg0})";
        }
    }
}