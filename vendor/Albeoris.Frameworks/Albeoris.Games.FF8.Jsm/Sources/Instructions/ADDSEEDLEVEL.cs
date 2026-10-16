using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class ADDSEEDLEVEL : JsmInstruction
    {
        private IJsmExpression _arg0;

        public ADDSEEDLEVEL(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public ADDSEEDLEVEL(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(ADDSEEDLEVEL)}({nameof(_arg0)}: {_arg0})";
        }
    }
}