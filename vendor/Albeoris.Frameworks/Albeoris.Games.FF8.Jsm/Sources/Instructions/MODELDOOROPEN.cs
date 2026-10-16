using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MODELDOOROPEN : JsmInstruction
    {
        private IJsmExpression _side;

        public MODELDOOROPEN(IJsmExpression side)
        {
            _side = side;
        }

        public MODELDOOROPEN(Int32 parameter, IExpressionStack stack)
            : this(
                side: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MODELDOOROPEN)}({nameof(_side)}: {_side})";
        }
    }
}