using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MLIMIT : JsmInstruction
    {
        private IJsmExpression _arg0;

        public MLIMIT(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public MLIMIT(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MLIMIT)}({nameof(_arg0)}: {_arg0})";
        }
    }
}