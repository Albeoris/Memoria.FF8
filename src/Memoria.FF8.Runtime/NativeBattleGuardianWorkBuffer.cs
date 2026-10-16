using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>571B70's separate retained209FAB8..20DFAB8 work bank;571870 never clears it.</summary>
public sealed record NativeBattleGuardianWorkBuffer([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> Regions)
{
    public const int Length = 0x40000;
    public static NativeBattleGuardianWorkBuffer FromColdProcess() => new([]);

    public void Validate()
    {
        new NativeBattleEffectArena(Regions).Validate();
        if (Regions.Any(region => region.Offset > Length - region.Data.Length))
        {
            throw new InvalidDataException("Native571B70 work memory exceeds209FAB8..20DFAB8.");
        }
    }

    public byte[] Read(int offset, int count)
    {
        RequireRange(offset, count);
        Validate();
        return new NativeBattleEffectArena(Regions).Read(offset, count);
    }

    public NativeBattleGuardianWorkBuffer Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        Validate();
        return new(new NativeBattleEffectArena(Regions).Write(offset, bytes).Regions);
    }

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native571B70 access leaves its separate262144-byte work buffer.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleGuardianWorkBuffer? GuardianWorkBuffer { get; init; }
    public NativeBattleEffectIoState? EffectIo { get; init; }
    public byte? GuardianCasterBank { get; init; }
    public int? QuezacotlPause { get; init; }
    public ImmutableArray<uint>? QuezacotlSecondaryHeads { get; init; }
}

/// <summary>Process1DCD6E8 callback; controller offsets are logical pointers into the current effect arena.</summary>
public sealed record NativeBattleEffectIoState(uint Callback, int? ControllerOffset, int? BaseOffset)
{
    public static NativeBattleEffectIoState FromColdProcess() => new(0, null, null);
}
