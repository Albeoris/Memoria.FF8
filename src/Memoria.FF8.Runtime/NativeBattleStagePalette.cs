using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>506CF0's observed512-byte write and DWORD1D98A48 publication; this is not a general retained packet-bank byte reader.</summary>
public sealed record NativeBattleStagePaletteAllocation(NativeBattlePrimitiveReference Address, NativeBattleEffectHeapAllocation Source);

public static class NativeBattleStagePalette
{
    public static (NativeBattlePrimitiveProcess Process, NativeBattleStagePaletteAllocation Allocation) Write506CF0(NativeBattlePrimitiveProcess process)
    {
        process.Validate();
        string bank = process.PacketBank ?? throw new NotSupportedException("Native506CF0 palette requires the allocated native packet bank.");
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Native506CF0 palette requires the observed native packet cursor.");
        NativeBattlePrimitiveProcess next = process with { ByteCursor = checked(cursor + 512) };
        next.Validate();
        byte[] bytes = new byte[512];
        for (int index = 1; index < 256; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2 * index), 0xC210);
        }
        NativeBattleEffectHeapAllocation source = new($"506CF0:{bank}:{process.PacketBuffer}:{cursor}", bytes.ToImmutableArray());
        return (next, new(new(bank, process.PacketBuffer, cursor), source));
    }
}
