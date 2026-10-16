using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class GAMEOVER : JsmInstruction
    {
        public GAMEOVER()
        {
        }

        public GAMEOVER(Int32 parameter, IExpressionStack stack)
            : this()
        {
        }

        public override String ToString()
        {
            return $"{nameof(GAMEOVER)}()";
        }
    }
}