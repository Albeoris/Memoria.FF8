using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectTextureState
{
    private NativeBattleVramOwner vramOwner = new(VramRegions);
    private NativeBattleVramVersion? vramVersion;
    private ImmutableArray<NativeBattleEffectMemoryRegion> snapshotRegions = VramRegions;
    private bool textureSnapshot;
    private NativeBattleVramVersion VramVersion => vramVersion ?? vramOwner.Initial;

    [JsonRequired]
    public ImmutableArray<NativeBattleEffectMemoryRegion> VramRegions
    {
        get => textureSnapshot ? snapshotRegions : throw new InvalidOperationException("VRAM regions require an explicit detached texture capture.");
        init
        {
            RequireWorking();
            snapshotRegions = value;
            vramOwner = new(value);
            vramVersion = null;
        }
    }

    bool INativeBattleStorageNode.IsSnapshot => textureSnapshot;

    private void RequireWorking()
    {
        if (textureSnapshot)
        {
            throw new InvalidOperationException("Detached texture snapshots must be restored before mutation.");
        }
    }

    private void ValidateVram()
    {
        if (textureSnapshot)
        {
            NativeBattleEffectArena.CreateSnapshot(snapshotRegions, []).Validate();
        }
    }

    [JsonIgnore]
    public object VramPublicationIdentity => VramVersion;

    public void ReadVramInto(int offset, Span<byte> destination) => VramVersion.ReadInto(offset, destination);

    public ushort ReadVramWord(int x, int y)
    {
        if (x is < 0 or >= 1024 || y is < 0 or >= 512)
        {
            throw new InvalidDataException("Native texture sampling leaves its physical VRAM extent.");
        }
        return VramVersion.ReadWord(2 * (y * 1024 + x));
    }

    public NativeBattleEffectTextureState PublishVramSpan(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireWorking();
        using NativeBattleVramOwner.Mutation mutation = vramOwner.BeginMutation(VramVersion);
        mutation.Write(offset, bytes);
        return this with { vramVersion = mutation.Commit() };
    }

    internal NativeBattleEffectTextureState WithVramPublication(NativeBattleEffectTextureState source)
    {
        RequireWorking();
        source.RequireWorking();
        if (!ReferenceEquals(vramOwner, source.vramOwner))
        {
            throw new InvalidOperationException("VRAM publication transfer requires the same physical owner.");
        }
        return this with { vramVersion = source.VramVersion };
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        if (vramOwner.Arena.Working.HasCheckpoint)
        {
            throw new InvalidOperationException("Texture capture requires a committed physical VRAM owner.");
        }
        return CreateSnapshot(Slots, Count, Animation, VramVersion.CaptureRegions(), VramReadTail);
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        NativeBattleEffectTextureState result = new(Slots, Count, Animation, snapshotRegions) { VramReadTail = VramReadTail };
        _ = result.vramOwner.Arena.Working;
        return result;
    }

    internal static NativeBattleEffectTextureState CreateSnapshot(ImmutableArray<NativeBattleEffectTimUpload?> slots, int count, NativeBattleEffectAnimationRegisters? animation, ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<byte>? tail)
    {
        NativeBattleEffectTextureState result = new(slots, count, animation, regions) { VramReadTail = tail };
        result.vramVersion = result.vramOwner.Initial;
        result.vramOwner = null!;
        result.textureSnapshot = true;
        result.Validate();
        return result;
    }
}

public sealed class NativeBattleEffectTextureStateJsonConverter : JsonConverter<NativeBattleEffectTextureState>
{
    private sealed record SnapshotData([property: JsonRequired] ImmutableArray<NativeBattleEffectTimUpload?> Slots, [property: JsonRequired] int Count, [property: JsonRequired] NativeBattleEffectAnimationRegisters? Animation, [property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> VramRegions)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<byte>? VramReadTail { get; init; }
    }

    public override NativeBattleEffectTextureState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        SnapshotData value = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new JsonException("Texture snapshot is missing.");
        return NativeBattleEffectTextureState.CreateSnapshot(value.Slots, value.Count, value.Animation, value.VramRegions, value.VramReadTail);
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleEffectTextureState value, JsonSerializerOptions options)
    {
        if (!((INativeBattleStorageNode)value).IsSnapshot)
        {
            throw new InvalidOperationException("Working textures require an explicit detached capture before serialization.");
        }
        value.Validate();
        JsonSerializer.Serialize(writer, new SnapshotData(value.Slots, value.Count, value.Animation, value.VramRegions) { VramReadTail = value.VramReadTail }, options);
    }
}
