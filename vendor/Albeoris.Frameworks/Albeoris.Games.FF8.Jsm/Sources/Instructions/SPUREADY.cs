using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SPUREADY : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SPUREADY(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SPUREADY(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SPUREADY)}({nameof(_arg0)}: {_arg0})";
        }
    }
}