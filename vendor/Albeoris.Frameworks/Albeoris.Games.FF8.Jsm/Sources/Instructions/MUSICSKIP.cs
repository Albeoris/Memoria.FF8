using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MUSICSKIP : JsmInstruction
    {
        private IJsmExpression _arg0;

        public MUSICSKIP(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public MUSICSKIP(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MUSICSKIP)}({nameof(_arg0)}: {_arg0})";
        }
    }
}