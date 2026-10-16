using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FADENONE : JsmInstruction
    {
        public FADENONE()
        {
        }

        public FADENONE(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FADENONE)}()";
        }
    }
}