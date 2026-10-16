using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class DOORLINEOFF : JsmInstruction
    {
        public DOORLINEOFF()
        {
        }

        public DOORLINEOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(DOORLINEOFF)}()";
        }
    }
}