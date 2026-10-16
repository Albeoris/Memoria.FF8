namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldSalaryState
{
    public NativeWorldEntryMemory? WorldEntryMemory { get; private init; }

    /// <summary>Stage543750's actual event264.bit40 write after54A7F0 consumes its request, even if placement fails.</summary>
    public NativeWorldSalaryState ConsumeGardenScriptRequest()
    {
        NativeWorldEntryMemory memory = WorldEntryMemory ?? throw new NotSupportedException("Garden request consumption requires its current transaction-owned world-entry bytes.");
        memory.Validate();
        if ((memory.ReplacementFlags264 & 0x40) == 0)
        {
            return this;
        }
        return this with { WorldEntryMemory = memory.WriteBytes(264, 1, memory.ReplacementFlags264 & ~0x40) };
    }
}
