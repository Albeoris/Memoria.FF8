using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class JOIN : JsmInstruction
    {
        public JOIN()
        {
        }

        public JOIN(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(JOIN)}()";
        }
    }
}