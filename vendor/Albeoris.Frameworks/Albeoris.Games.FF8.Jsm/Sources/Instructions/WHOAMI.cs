using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class WHOAMI : JsmInstruction
    {
        private IJsmExpression _arg0;

        public WHOAMI(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public WHOAMI(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(WHOAMI)}({nameof(_arg0)}: {_arg0})";
        }
    }
}