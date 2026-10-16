using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class EFFECTLOAD : JsmInstruction
    {
        private IJsmExpression _arg0;

        public EFFECTLOAD(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public EFFECTLOAD(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(EFFECTLOAD)}({nameof(_arg0)}: {_arg0})";
        }
    }
}