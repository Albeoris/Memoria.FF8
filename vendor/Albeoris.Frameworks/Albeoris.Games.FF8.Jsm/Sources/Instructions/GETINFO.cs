using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class GETINFO : JsmInstruction
    {
        public GETINFO()
        {
        }

        public GETINFO(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(GETINFO)}()";
        }
    }
}