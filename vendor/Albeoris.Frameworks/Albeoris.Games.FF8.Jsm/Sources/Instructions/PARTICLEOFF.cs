using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class PARTICLEOFF : JsmInstruction
    {
        private IJsmExpression _arg0;

        public PARTICLEOFF(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public PARTICLEOFF(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(PARTICLEOFF)}({nameof(_arg0)}: {_arg0})";
        }
    }
}