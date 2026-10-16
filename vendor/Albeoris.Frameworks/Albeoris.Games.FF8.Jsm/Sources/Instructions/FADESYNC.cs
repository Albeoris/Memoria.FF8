using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FADESYNC : JsmInstruction
    {
        public FADESYNC()
        {
        }

        public FADESYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FADESYNC)}()";
        }
    }
}