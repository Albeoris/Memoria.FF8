using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class PARTICLESET : JsmInstruction
    {
        private IJsmExpression _arg0;

        public PARTICLESET(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public PARTICLESET(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(PARTICLESET)}({nameof(_arg0)}: {_arg0})";
        }
    }
}