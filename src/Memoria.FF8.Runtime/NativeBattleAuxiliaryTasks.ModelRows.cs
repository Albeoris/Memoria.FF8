namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    private static uint WriteModelOperand(byte[] payload, uint unknown, int offset, BattleModelOwner owner, NativeBattleModelAliases aliases)
    {
        uint mask = 15u << offset;
        if (aliases.EncodeAddress(owner) is not { } encoded)
        {
            return unknown | mask;
        }
        encoded.AsSpan().CopyTo(payload.AsSpan(offset, 4));
        return unknown & ~mask;
    }

    private static BattleModelOwner RequireModelOperand(BattleModelOwner? owner)
    {
        return owner ?? throw new InvalidDataException("The active auxiliary task has no model owner.");
    }
}
