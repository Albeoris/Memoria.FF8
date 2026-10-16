using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SEALEDOFF : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SEALEDOFF(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SEALEDOFF(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SEALEDOFF)}({nameof(_arg0)}: {_arg0})";
        }
    }
}