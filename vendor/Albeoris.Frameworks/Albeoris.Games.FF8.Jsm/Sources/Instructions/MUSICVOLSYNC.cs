using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MUSICVOLSYNC : JsmInstruction
    {
        public MUSICVOLSYNC()
        {
        }

        public MUSICVOLSYNC(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MUSICVOLSYNC)}()";
        }
    }
}