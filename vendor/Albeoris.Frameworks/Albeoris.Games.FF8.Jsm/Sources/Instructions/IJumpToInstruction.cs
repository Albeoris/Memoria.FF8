using System;

namespace Albeoris.Games.FF8.Jsm.Instructions
{
    public interface IJumpToInstruction : IJsmInstruction
    {
        Int32 Index { get; set; }
    }
}