using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class Unknown1 : JsmInstruction
    {
        private IJsmExpression _arg0;

        public Unknown1(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public Unknown1(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(Unknown1)}({nameof(_arg0)}: {_arg0})";
        }
    }
}