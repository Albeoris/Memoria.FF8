using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FOOTSTEP : JsmInstruction
    {
        private Int32 _parameter;
        private IJsmExpression _arg0;

        public FOOTSTEP(Int32 parameter, IJsmExpression arg0)
        {
            _parameter = parameter;
            _arg0 = arg0;
        }

        public FOOTSTEP(Int32 parameter, IExpressionStack stack)
            : this(parameter,
                arg0: stack.Pop())
        {
        }

        public override String ToString()
        {
            return $"{nameof(FOOTSTEP)}({nameof(_parameter)}: {_parameter}, {nameof(_arg0)}: {_arg0})";
        }
    }
}