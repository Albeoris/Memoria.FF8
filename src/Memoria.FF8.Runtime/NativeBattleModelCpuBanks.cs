using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated party CPU allocation; saved bounds are derived from the image identity.</summary>
public sealed record NativeBattlePartyCpuLayout([property: JsonRequired] string ExecutableHash)
{
    public static NativeBattlePartyCpuLayout Classic { get; } = new(NativeFieldRuntimeProfile.FF8EnExecutableHash);
    public static NativeBattlePartyCpuLayout Remastered { get; } = new(NativeFieldRuntimeProfile.RemasteredEnImageHash);

    public static NativeBattlePartyCpuLayout ForImage(string executableHash)
    {
        NativeBattlePartyCpuLayout layout = new(executableHash);
        layout.Validate();
        return layout;
    }

    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash))
        {
            throw new InvalidDataException("Native party CPU allocation requires an authenticated supported image.");
        }
    }

    [JsonIgnore]
    public uint BaseAddress => ExecutableHash switch
    {
        NativeFieldRuntimeProfile.FF8EnExecutableHash => 0x0196B018u,
        NativeFieldRuntimeProfile.RemasteredEnImageHash => 0x0A100000u,
        _ => throw new InvalidDataException("Unknown native party CPU allocation.")
    };

    [JsonIgnore]
    public int BankLength => ExecutableHash switch
    {
        NativeFieldRuntimeProfile.FF8EnExecutableHash => 0x10000,
        NativeFieldRuntimeProfile.RemasteredEnImageHash => 0x20000,
        _ => throw new InvalidDataException("Unknown native party CPU allocation.")
    };
}

public sealed partial record NativeBattleModelCpuReference(byte Bank, int Offset)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePartyCpuLayout? Layout { get; init; }

    [JsonIgnore]
    public NativeBattlePartyCpuLayout EffectiveLayout => Layout ?? NativeBattlePartyCpuLayout.Classic;

    [JsonIgnore]
    public uint PhysicalAddress
    {
        get
        {
            NativeBattlePartyCpuLayout layout = EffectiveLayout;
            layout.Validate();
            return Bank < 3 ? checked(layout.BaseAddress + (uint)layout.BankLength * Bank + (uint)Offset) : SharedAddress;
        }
    }
}

/// <summary>One bounded sparse CPU bank. Unwritten heap bytes retain their unknown owner.</summary>
public sealed record NativeBattleModelCpuBank(ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    private readonly NativeBattleModelResourceStorage storage = NativeBattleModelResourceStorage.FromKnownRanges(KnownRanges);

    /// <summary>Compatible diagnostic materialization; ordinary resource reads do not construct or retain this image.</summary>
    public ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges
    {
        get => storage.CaptureKnownRanges(Capacity);
        init => storage = NativeBattleModelResourceStorage.FromKnownRanges(value);
    }

    public const int Length = 0x10000;
    public int Capacity { get; init; } = Length;
    internal ImmutableArray<NativeBattleModelResourceSegment> ResourceSegments => storage.Segments;
    public static NativeBattleModelCpuBank Unknown(int capacity = Length) => new([]) { Capacity = capacity };

    public byte[] Read(int offset, int length)
    {
        return storage.Read(offset, length, Capacity);
    }

    public NativeBattleModelCpuBank Write(int offset, ReadOnlySpan<byte> bytes)
    {
        NativeBattleModelResourceStorage updated = storage.Write(offset, bytes, Capacity);
        return ReferenceEquals(updated, storage) ? this : new(updated, Capacity);
    }

    internal NativeBattleModelCpuBank BindResource(int offset, ImmutableArray<byte> source, int sourceOffset, int length)
    {
        NativeBattleModelResourceStorage updated = storage.Bind(offset, source, sourceOffset, length, Capacity);
        return ReferenceEquals(updated, storage) ? this : new(updated, Capacity);
    }

    private NativeBattleModelCpuBank(NativeBattleModelResourceStorage storage, int capacity) : this([])
    {
        this.storage = storage;
        Capacity = capacity;
    }
}

public sealed record NativeBattleModelCpuBanks(ImmutableArray<NativeBattleModelCpuBank> Banks)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePartyCpuLayout? Layout { get; init; }

    [JsonIgnore]
    public NativeBattlePartyCpuLayout EffectiveLayout => Layout ?? NativeBattlePartyCpuLayout.Classic;

    public static NativeBattleModelCpuBanks Unknown(NativeBattlePartyCpuLayout? layout = null)
    {
        NativeBattlePartyCpuLayout effective = layout ?? NativeBattlePartyCpuLayout.Classic;
        effective.Validate();
        return new([NativeBattleModelCpuBank.Unknown(effective.BankLength), NativeBattleModelCpuBank.Unknown(effective.BankLength), NativeBattleModelCpuBank.Unknown(effective.BankLength)]) { Layout = layout };
    }

    public void Validate()
    {
        NativeBattlePartyCpuLayout layout = EffectiveLayout;
        layout.Validate();
        if (Banks.IsDefault || Banks.Length != 3 || Banks.Any(bank => bank is null || bank.Capacity != layout.BankLength))
        {
            throw new InvalidDataException("Native party CPU banks disagree with their authenticated allocation.");
        }
        foreach (NativeBattleModelCpuBank bank in Banks)
        {
            _ = bank.Read(0, 0);
        }
    }

    public NativeBattleModelCpuBanks CopyBank50B2A0(byte source, byte destination)
    {
        Validate();
        if (source >= 3 || destination >= 3)
        {
            throw new InvalidDataException("Native50B2A0 CPU copy requires its three physical party banks.");
        }
        // memcpy transports unknown retained bytes as unknown too, erasing prior destination knowledge.
        return this with { Banks = Banks.SetItem(destination, Banks[source]) };
    }
}
