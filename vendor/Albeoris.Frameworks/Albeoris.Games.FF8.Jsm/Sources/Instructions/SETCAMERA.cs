using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class SETCAMERA : JsmInstruction
    {
        private IJsmExpression _arg0;

        public SETCAMERA(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public SETCAMERA(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(SETCAMERA)}({nameof(_arg0)}: {_arg0})";
        }
    }
}