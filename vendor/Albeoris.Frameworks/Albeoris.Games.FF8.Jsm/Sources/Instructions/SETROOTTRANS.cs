using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SETROOTTRANS : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SETROOTTRANS(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SETROOTTRANS(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SETROOTTRANS)}({nameof(_arg0)}: {_arg0})";
        }
    }
}