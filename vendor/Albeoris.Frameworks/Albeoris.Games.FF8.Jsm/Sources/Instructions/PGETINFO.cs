using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class PGETINFO : JsmInstruction
    {
        private IJsmExpression _arg0;

        public PGETINFO(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public PGETINFO(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(PGETINFO)}({nameof(_arg0)}: {_arg0})";
        }
    }
}