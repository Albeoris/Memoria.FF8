using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class BGSHADEOFF : JsmInstruction
    {
        public BGSHADEOFF()
        {
        }

        public BGSHADEOFF(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(BGSHADEOFF)}()";
        }
    }
}