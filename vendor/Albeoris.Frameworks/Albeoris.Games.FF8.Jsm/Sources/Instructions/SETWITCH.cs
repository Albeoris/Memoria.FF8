using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SETWITCH : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SETWITCH(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SETWITCH(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SETWITCH)}({nameof(_arg0)}: {_arg0})";
        }
    }
}