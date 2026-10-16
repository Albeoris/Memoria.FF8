using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class IDUNLOCK : JsmInstruction
    {
        private Int32 _parameter;

        public IDUNLOCK(Int32 parameter)
        {
            _parameter = parameter;
        }

        public IDUNLOCK(Int32 parameter, IExpressionStack stack)
            : this(parameter)
        {
        }

        public override String ToString()
        {
            return $"{nameof(IDUNLOCK)}({nameof(_parameter)}: {_parameter})";
        }
    }
}