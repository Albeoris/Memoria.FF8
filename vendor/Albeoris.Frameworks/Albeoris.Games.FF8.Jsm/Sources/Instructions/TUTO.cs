using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class TUTO : JsmInstruction
    {
        private IJsmExpression _arg0;

        public TUTO(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public TUTO(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(TUTO)}({nameof(_arg0)}: {_arg0})";
        }
    }
}