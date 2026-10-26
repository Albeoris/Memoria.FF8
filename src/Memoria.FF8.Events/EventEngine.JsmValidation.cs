using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Uses the same opcode gate as ordinary interpreter execution; runtime ownership checks still apply.</summary>
    public static bool SupportsInstructionOpcode(Jsm.Opcode opcode) => Supports(opcode);
}
