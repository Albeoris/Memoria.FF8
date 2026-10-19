using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModelCpuReference(byte Bank, int Offset)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public uint PhysicalAddress => Bank < 3 ? checked(0x196B018u + 0x10000u * Bank + (uint)Offset) : SharedAddress;
}

/// <summary>One mutable64KB party bank. Unwritten heap bytes are unknown, unlike the cleared effect arena.</summary>
public sealed record NativeBattleModelCpuBank(ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    public const int Length = 0x10000;
    public static NativeBattleModelCpuBank Unknown() => new([]);

    public byte[] Read(int offset, int length)
    {
        return NativeKnownByteRanges.Read(KnownRanges, offset, length, Length);
    }

    public NativeBattleModelCpuBank Write(int offset, ReadOnlySpan<byte> bytes)
    {
        return new(NativeKnownByteRanges.Write(KnownRanges, offset, bytes, Length));
    }
}

public sealed record NativeBattleModelCpuBanks(ImmutableArray<NativeBattleModelCpuBank> Banks)
{
    public static NativeBattleModelCpuBanks Unknown() => new([NativeBattleModelCpuBank.Unknown(), NativeBattleModelCpuBank.Unknown(), NativeBattleModelCpuBank.Unknown()]);

    public NativeBattleModelCpuBanks Copy64K50B2A0(byte source, byte destination)
    {
        if (Banks.IsDefault || Banks.Length != 3 || source >= 3 || destination >= 3)
        {
            throw new InvalidDataException("Native50B2A0 CPU copy requires its three physical party banks.");
        }
        //memcpy transports unknown retained bytes as unknown too, erasing any prior destination knowledge there.
        _ = Banks[source].Read(0, 0);
        return this with { Banks = Banks.SetItem(destination, new(Banks[source].KnownRanges)) };
    }
}
