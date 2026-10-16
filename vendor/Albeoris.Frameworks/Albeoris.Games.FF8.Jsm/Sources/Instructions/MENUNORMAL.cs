using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class MENUNORMAL : JsmInstruction
    {
        public MENUNORMAL()
        {
        }

        public MENUNORMAL(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(MENUNORMAL)}()";
        }
    }
}