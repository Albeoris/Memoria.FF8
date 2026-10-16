using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class KEYSCAN2 : JsmInstruction
    {
        private IJsmExpression _arg0;

        public KEYSCAN2(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public KEYSCAN2(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(KEYSCAN2)}({nameof(_arg0)}: {_arg0})";
        }
    }
}