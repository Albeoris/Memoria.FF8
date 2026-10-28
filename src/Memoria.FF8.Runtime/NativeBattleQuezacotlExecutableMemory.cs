using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One physical owner for the adjacent retained executable model regions.</summary>
[JsonConverter(typeof(NativeBattleQuezacotlExecutableMemory.Converter))]
public sealed record NativeBattleQuezacotlExecutableMemory : INativeBattleStorageNode
{
    public const uint Address = 0x1298C68;
    public const int PrimaryLength = 0x109FC;
    public const int SecondaryLength = 0x4D9C;
    public const int Length = PrimaryLength + SecondaryLength;
    private readonly NativeBattleEffectArena arena;
    private readonly bool snapshot;
    private readonly ImmutableArray<byte> data;

    public NativeBattleQuezacotlExecutableMemory(ImmutableArray<byte> Data)
    {
        RequireData(Data);
        arena = NativeBattleEffectArena.CreateUnknown(Length).Write(0, Data.AsSpan());
    }

    private NativeBattleQuezacotlExecutableMemory(NativeBattleEffectArena arena, bool snapshot, ImmutableArray<byte> data = default)
    {
        this.arena = arena;
        this.snapshot = snapshot;
        this.data = data;
    }

    [JsonRequired]
    public ImmutableArray<byte> Data => snapshot ? data : throw new InvalidOperationException("Executable model bytes require an explicit detached snapshot.");
    [JsonIgnore]
    public NativeBattleEffectArena Arena => !snapshot ? arena : throw new InvalidOperationException("Frozen executable model storage must be restored before binding.");
    bool INativeBattleStorageNode.IsSnapshot => snapshot;

    public void Validate() => arena.Validate();

    public byte[] Read(uint address, int length) => arena.Read(Offset(address, length), length);
    public void ReadInto(uint address, Span<byte> destination) => arena.ReadInto(Offset(address, destination.Length), destination);
    public void ReadWithKnownness(uint address, Span<byte> destination, Span<bool> known) => arena.ReadWithKnownness(Offset(address, destination.Length), destination, known);
    public byte ReadByte(uint address) => arena.ReadByte(Offset(address, 1));
    public ushort ReadUInt16(uint address) => arena.ReadUInt16(Offset(address, 2));
    public uint ReadUInt32(uint address) => arena.ReadUInt32(Offset(address, 4));

    public NativeBattleQuezacotlExecutableMemory Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (snapshot)
        {
            throw new InvalidOperationException("Frozen executable model storage must be restored before mutation.");
        }
        arena.Write(Offset(address, bytes.Length), bytes);
        return this;
    }

    /// <summary>The native cleanup copies the actual backup ranges back after retiring their task views.</summary>
    public NativeBattleQuezacotlExecutableMemory Restore6C5196(NativeBattleGuardianWorkBuffer backup)
    {
        backup.Validate();
        return Write(Address, backup.Read(0, Length));
    }

    private static int Offset(uint address, int length)
    {
        if (address < Address || length < 0 || (ulong)address - Address + (uint)length > Length)
        {
            throw new InvalidDataException("Native Quezacotl executable access leaves its owned model regions.");
        }
        return checked((int)(address - Address));
    }

    private static void RequireData(ImmutableArray<byte> data)
    {
        if (data.IsDefault || data.Length != Length)
        {
            throw new InvalidDataException("Native Quezacotl requires its exact two retained executable model regions.");
        }
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        NativeBattleEffectArena detached = context.Map(arena);
        ImmutableArray<NativeBattleEffectMemoryRegion> regions = detached.Regions;
        ImmutableArray<byte> payload = regions.Length == 1 && regions[0].Offset == 0 && regions[0].Data.Length == Length ? regions[0].Data : System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(detached.ReadSnapshot(0, Length).Data);
        return new NativeBattleQuezacotlExecutableMemory(detached, true, payload);
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context) => new NativeBattleQuezacotlExecutableMemory(context.Map(arena), false);

    private sealed record SnapshotData([property: JsonRequired] ImmutableArray<byte> Data)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownRegions { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers { get; init; }
    }

    public sealed class Converter : JsonConverter<NativeBattleQuezacotlExecutableMemory>
    {
        public override NativeBattleQuezacotlExecutableMemory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            SnapshotData value = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new InvalidDataException("Executable model snapshot is missing.");
            RequireData(value.Data);
            NativeBattleEffectArena frozen = new([new(0, value.Data)], value.UnknownRegions ?? [], value.ResourcePointers, Length);
            frozen.Validate();
            return new(frozen, true, value.Data);
        }

        public override void Write(Utf8JsonWriter writer, NativeBattleQuezacotlExecutableMemory value, JsonSerializerOptions options)
        {
            if (!value.snapshot)
            {
                throw new InvalidOperationException("Executable model serialization requires an explicit detached snapshot.");
            }
            JsonSerializer.Serialize(writer, new SnapshotData(value.data)
            {
                UnknownRegions = value.arena.UnknownRegions.IsEmpty ? null : value.arena.UnknownRegions,
                ResourcePointers = value.arena.ResourcePointers is { Count: > 0 } pointers ? pointers : null
            }, options);
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleQuezacotlExecutableMemory? QuezacotlExecutableMemory
    {
        get => effectState.QuezacotlExecutableMemory;
        init => effectState = effectState with { QuezacotlExecutableMemory = value };
    }
}
