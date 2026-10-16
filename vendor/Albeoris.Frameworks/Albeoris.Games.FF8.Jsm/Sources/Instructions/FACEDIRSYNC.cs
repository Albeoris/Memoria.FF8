using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class FACEDIRSYNC : JsmInstruction
    {
        public FACEDIRSYNC()
        {
        }

        public FACEDIRSYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(FACEDIRSYNC)}()";
        }
    }
}