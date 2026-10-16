using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FACEDIROFF : JsmInstruction
    {
        private IJsmExpression _arg0;

        public FACEDIROFF(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public FACEDIROFF(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(FACEDIROFF)}({nameof(_arg0)}: {_arg0})";
        }
    }
}