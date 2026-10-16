using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    public interface IJumpToOpcode : IJumpToInstruction
    {
        Int32 Offset { get; }
    }
}