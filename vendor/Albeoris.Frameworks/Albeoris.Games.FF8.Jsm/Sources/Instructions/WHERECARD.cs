using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class WHERECARD : JsmInstruction
    {
        private IJsmExpression _arg0;

        public WHERECARD(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public WHERECARD(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(WHERECARD)}({nameof(_arg0)}: {_arg0})";
        }
    }
}