namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldSalaryState
{
    public NativeWorldSalaryState WithWorldEventCommand(ushort opcode, ushort operand)
    {
        NativeSavedWorldState saved = SavedWorldEvents ?? throw new NotSupportedException("Native world event commands require their actual saved-world owner.");
        NativeSavedWorldState next = opcode switch
        {
            0xFF28 => saved with { WorldEventFlags116 = (saved.WorldEventFlags116 ?? new NativeWorldEventFlags(0, 0)).WithOperand(operand) },
            0xFF2E => saved.WithWorldEventVariable(unchecked((byte)operand), unchecked((byte)(operand >> 8))),
            _ => throw new NotSupportedException($"Unsupported saved-world event command{opcode:X4}.")
        };
        return this with { SavedWorldEvents = next };
    }

    private NativeSavedWorldState? ApplySavedWorldOwners(NativeSavedWorldState? saved)
    {
        if (saved is null)
        {
            return null;
        }
        return saved with
        {
            FuelDistance112 = FuelDistance112,
            WorldEventFlags116 = SavedWorldEvents?.WorldEventFlags116,
            WorldEventVariable124 = SavedWorldEvents?.WorldEventVariable124,
            WorldEventVariable125 = SavedWorldEvents?.WorldEventVariable125
        };
    }
}
