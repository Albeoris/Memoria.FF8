using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class KEYON : JsmInstruction
    {
        private IJsmExpression _arg0;

        public KEYON(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public KEYON(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(KEYON)}({nameof(_arg0)}: {_arg0})";
        }
    }
}