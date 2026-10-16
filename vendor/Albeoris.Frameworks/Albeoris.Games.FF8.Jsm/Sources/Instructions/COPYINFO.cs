using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class COPYINFO : JsmInstruction
    {
        private IJsmExpression _arg0;

        public COPYINFO(IJsmExpression arg0)
        {
            _arg0 = arg0;
        }

        public COPYINFO(Int32 parameter, IExpressionStack stack)
            : this(
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(COPYINFO)}({nameof(_arg0)}: {_arg0})";
        }
    }
}