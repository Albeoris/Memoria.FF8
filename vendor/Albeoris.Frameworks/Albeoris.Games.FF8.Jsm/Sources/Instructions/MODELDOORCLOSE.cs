using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MODELDOORCLOSE : JsmInstruction
    {
        private IJsmExpression _side;

        public MODELDOORCLOSE(IJsmExpression side)
        {
            _side = side;
        }

        public MODELDOORCLOSE(Int32 parameter, IExpressionStack stack)
            : this(
                side: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MODELDOORCLOSE)}({nameof(_side)}: {_side})";
        }
    }
}