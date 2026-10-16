using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class LOADSYNC : JsmInstruction
    {
        public LOADSYNC()
        {
        }

        public LOADSYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(LOADSYNC)}()";
        }
    }
}