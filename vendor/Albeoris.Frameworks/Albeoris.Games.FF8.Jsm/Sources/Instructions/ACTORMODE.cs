using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class ACTORMODE : JsmInstruction
    {
        private IJsmExpression _arg0;

        public ACTORMODE(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public ACTORMODE(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(ACTORMODE)}({nameof(_arg0)}: {_arg0})";
        }
    }
}