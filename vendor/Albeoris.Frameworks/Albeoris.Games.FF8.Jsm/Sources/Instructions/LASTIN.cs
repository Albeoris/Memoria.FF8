using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class LASTIN : JsmInstruction
    {
        private IJsmExpression _arg0;

        public LASTIN(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public LASTIN(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(LASTIN)}({nameof(_arg0)}: {_arg0})";
        }
    }
}