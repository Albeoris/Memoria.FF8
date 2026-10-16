using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class TALKRADIUS : JsmInstruction
    {
        private IJsmExpression _arg0;

        public TALKRADIUS(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public TALKRADIUS(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(TALKRADIUS)}({nameof(_arg0)}: {_arg0})";
        }
    }
}