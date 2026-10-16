using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    internal sealed class RANIMELOOP : JsmInstruction
    {
        private Int32 _parameter;

        public RANIMELOOP(Int32 parameter)
        {
            _parameter = parameter;
        }

        public RANIMELOOP(Int32 parameter, IExpressionStack stack)
            : this(parameter)
        {
        }

        public override String ToString()
        {
            return $"{nameof(RANIMELOOP)}({nameof(_parameter)}: {_parameter})";
        }
    }
}