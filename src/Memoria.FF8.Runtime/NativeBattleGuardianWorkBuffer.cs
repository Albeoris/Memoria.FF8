using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>571B70's separate retained209FAB8..20DFAB8 work bank;571870 never clears it.</summary>
[JsonConverter(typeof(NativeBattleGuardianWorkBufferJsonConverter))]
public sealed record NativeBattleGuardianWorkBuffer : INativeBattleStorageNode
{
    public const int Length = 0x40000;
    private readonly NativeBattleEffectArena arena;

    public NativeBattleGuardianWorkBuffer(ImmutableArray<NativeBattleEffectMemoryRegion> regions) => arena = new(regions);
    internal NativeBattleGuardianWorkBuffer(NativeBattleEffectArena arena) => this.arena = arena;
    public ImmutableArray<NativeBattleEffectMemoryRegion> Regions => arena.Regions;
    [JsonIgnore] public bool IsSnapshot => arena.IsSnapshot;
    [JsonIgnore] internal NativeBattleEffectArena Arena => arena;
    public static NativeBattleGuardianWorkBuffer FromColdProcess() => new(ImmutableArray<NativeBattleEffectMemoryRegion>.Empty);

    public void Validate() => arena.ValidateExtent(Length);

    public byte[] Read(int offset, int count)
    {
        RequireRange(offset, count);
        Validate();
        return arena.Read(offset, count);
    }

    public NativeBattleGuardianWorkBuffer Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        Validate();
        arena.Write(offset, bytes);
        return this;
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context) => new NativeBattleGuardianWorkBuffer(context.Map(arena));
    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => new NativeBattleGuardianWorkBuffer(context.Map(arena));

    private static void RequireRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native571B70 access leaves its separate262144-byte work buffer.");
        }
    }
}

internal sealed class NativeBattleGuardianWorkBufferJsonConverter : JsonConverter<NativeBattleGuardianWorkBuffer>
{
    public override NativeBattleGuardianWorkBuffer Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.ParseValue(ref reader);
        string name = options.PropertyNamingPolicy?.ConvertName("Regions") ?? "Regions";
        foreach (System.Text.Json.JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, name, options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                ImmutableArray<NativeBattleEffectMemoryRegion> regions = System.Text.Json.JsonSerializer.Deserialize<ImmutableArray<NativeBattleEffectMemoryRegion>>(property.Value, options);
                return new(NativeBattleEffectArena.CreateSnapshot(regions, []));
            }
        }
        throw new System.Text.Json.JsonException("Native work-buffer snapshot requires its regions.");
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, NativeBattleGuardianWorkBuffer value, System.Text.Json.JsonSerializerOptions options)
    {
        if (!value.IsSnapshot)
        {
            throw new InvalidOperationException("Native work-buffer serialization requires an explicit detached snapshot.");
        }
        writer.WriteStartObject();
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName("Regions") ?? "Regions");
        System.Text.Json.JsonSerializer.Serialize(writer, value.Regions, options);
        writer.WriteEndObject();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleGuardianWorkBuffer? GuardianWorkBuffer { get; init; }
    public NativeBattleEffectIoState? EffectIo { get; init; }
    public byte? GuardianCasterBank { get; init; }
    public int? QuezacotlPause
    {
        get => effectState.QuezacotlPause;
        init => effectState = effectState with { QuezacotlPause = value };
    }
    public ImmutableArray<uint>? QuezacotlSecondaryHeads
    {
        get => effectState.QuezacotlSecondaryHeads;
        init => effectState = effectState with { QuezacotlSecondaryHeads = value };
    }
}

/// <summary>Process1DCD6E8 callback; controller offsets are logical pointers into the current effect arena.</summary>
public sealed record NativeBattleEffectIoState(uint Callback, int? ControllerOffset, int? BaseOffset)
{
    public static NativeBattleEffectIoState FromColdProcess() => new(0, null, null);
}
