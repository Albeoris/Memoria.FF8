using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class IDLOCK : JsmInstruction
    {
        private Int32 _parameter;

        public IDLOCK(Int32 parameter)
        {
            _parameter = parameter;
        }

        public IDLOCK(Int32 parameter, IExpressionStack stack)
            : this(parameter)
        {
        }

        public override String ToString()
        {
            return $"{nameof(IDLOCK)}({nameof(_parameter)}: {_parameter})";
        }
    }
}