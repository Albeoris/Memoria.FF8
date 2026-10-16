using System;

namespace Albeoris.Games.FF8.Jsm.Core
{
    public interface IInteractionService
    {
        Boolean IsSupported { get; }

        Int32 this[ScriptResultId id] { get; set; }

        IAwaitable Wait(Int32 frameNumber);
    }
}