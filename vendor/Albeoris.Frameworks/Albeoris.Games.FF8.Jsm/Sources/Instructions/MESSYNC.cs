using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MESSYNC : JsmInstruction
    {
        private IJsmExpression _arg0;

        public MESSYNC(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public MESSYNC(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(MESSYNC)}({nameof(_arg0)}: {_arg0})";
        }
    }
}