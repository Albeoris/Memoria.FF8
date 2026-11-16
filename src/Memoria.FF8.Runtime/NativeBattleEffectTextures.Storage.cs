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

    [JsonIgnore]
    internal object VramCaptureIdentity => textureSnapshot ? VramVersion : vramOwner;

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
        return this with { vramVersion = mutation.Commit(), SceneCopies = NativeBattleSceneCopyProvenance.OverwriteBytes(SceneCopies, offset, bytes.Length) };
    }

    internal NativeBattleEffectTextureState WithVramPublication(NativeBattleEffectTextureState source)
    {
        RequireWorking();
        source.RequireWorking();
        if (!ReferenceEquals(vramOwner, source.vramOwner))
        {
            throw new InvalidOperationException("VRAM publication transfer requires the same physical owner.");
        }
        return this with { vramVersion = source.VramVersion, SceneCopies = source.SceneCopies };
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        ValidatePresentationTextures();
        if (vramOwner.Arena.Working.HasCheckpoint)
        {
            throw new InvalidOperationException("Texture capture requires a committed physical VRAM owner.");
        }
        return CreateSnapshot(Slots, Count, Animation, VramVersion.CaptureRegions(), VramReadTail, LiveTextureSources, TextureRegistrations, TextureFileContext, SceneCopies, context.Map(PresentationTextures));
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        ValidatePresentationTextures();
        // The global image is process memory. Each isolated model image belongs to one battle's model allocation.
        var presentationTextures = context.Purpose == NativeBattleStorageRestorePurpose.NewBattle ? null : context.Map(PresentationTextures);
        NativeBattleEffectTextureState result = new(Slots, Count, Animation, snapshotRegions) { VramReadTail = VramReadTail, LiveTextureSources = LiveTextureSources, TextureRegistrations = TextureRegistrations, TextureFileContext = TextureFileContext, SceneCopies = SceneCopies, PresentationTextures = presentationTextures };
        _ = result.vramOwner.Arena.Working;
        return result;
    }

    internal static NativeBattleEffectTextureState CreateSnapshot(ImmutableArray<NativeBattleEffectTimUpload?> slots, int count, NativeBattleEffectAnimationRegisters? animation, ImmutableArray<NativeBattleEffectMemoryRegion> regions, ImmutableArray<byte>? tail, ImmutableArray<NativeBattleTextureSourceRegion>? liveTextureSources = null, ImmutableArray<NativeBattleTextureRegistration>? textureRegistrations = null, NativeBattleTextureFileContext? textureFileContext = null, ImmutableArray<NativeBattleSceneCopyRegion>? sceneCopies = null, ImmutableDictionary<string, NativeBattlePresentationTextureBank>? presentationTextures = null)
    {
        NativeBattleEffectTextureState result = new(slots, count, animation, regions) { VramReadTail = tail, LiveTextureSources = liveTextureSources, TextureRegistrations = textureRegistrations, TextureFileContext = textureFileContext, SceneCopies = sceneCopies, PresentationTextures = presentationTextures };
        result.vramVersion = result.vramOwner.Initial;
        result.vramOwner = null!;
        result.textureSnapshot = true;
        result.Validate();
        return result;
    }
}

public sealed class NativeBattleEffectTextureStateJsonConverter : JsonConverter<NativeBattleEffectTextureState>
{
    private sealed record SharedPresentationTextureMember(string SourceActorId, NativeBattlePcTextureWindowCache Cache);

    private sealed record SnapshotData([property: JsonRequired] ImmutableArray<NativeBattleEffectTimUpload?> Slots, [property: JsonRequired] int Count, [property: JsonRequired] NativeBattleEffectAnimationRegisters? Animation, [property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> VramRegions)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<byte>? VramReadTail { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleTextureSourceRegion>? LiveTextureSources { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleTextureRegistration>? TextureRegistrations { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public NativeBattleTextureFileContext? TextureFileContext { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleSceneCopyRegion>? SceneCopies { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableDictionary<string, NativeBattlePresentationTextureBank>? PresentationTextures { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableDictionary<string, SharedPresentationTextureMember>? SharedPresentationTextureMembers { get; init; }
    }

    public override NativeBattleEffectTextureState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        SnapshotData value = JsonSerializer.Deserialize<SnapshotData>(ref reader, options) ?? throw new JsonException("Texture snapshot is missing.");
        var banks = value.PresentationTextures;
        if (value.SharedPresentationTextureMembers is { } members)
        {
            foreach ((string actorId, SharedPresentationTextureMember member) in members)
            {
                if (banks is null || banks.ContainsKey(actorId) || member is null || string.IsNullOrWhiteSpace(member.SourceActorId) ||
                    !value.PresentationTextures!.TryGetValue(member.SourceActorId, out NativeBattlePresentationTextureBank? source) || source?.EnemyAllocationId is null)
                {
                    throw new JsonException("Shared enemy texture member does not refer to a serialized allocation.");
                }
                banks = banks.Add(actorId, source with { Cache = member.Cache });
            }
        }
        return NativeBattleEffectTextureState.CreateSnapshot(value.Slots, value.Count, value.Animation, value.VramRegions, value.VramReadTail, value.LiveTextureSources, value.TextureRegistrations, value.TextureFileContext, value.SceneCopies, banks);
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleEffectTextureState value, JsonSerializerOptions options)
    {
        if (!((INativeBattleStorageNode)value).IsSnapshot)
        {
            throw new InvalidOperationException("Working textures require an explicit detached capture before serialization.");
        }
        value.Validate();
        var banks = value.PresentationTextures;
        var members = ImmutableDictionary.CreateBuilder<string, SharedPresentationTextureMember>(StringComparer.Ordinal);
        if (banks is not null)
        {
            Dictionary<int, string> sources = new();
            foreach ((string actorId, NativeBattlePresentationTextureBank bank) in banks.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (bank.EnemyAllocationId is not int allocation)
                {
                    continue;
                }
                if (sources.TryAdd(allocation, actorId))
                {
                    continue;
                }
                members.Add(actorId, new(sources[allocation], bank.Cache));
                banks = banks.Remove(actorId);
            }
        }
        JsonSerializer.Serialize(writer, new SnapshotData(value.Slots, value.Count, value.Animation, value.VramRegions)
        {
            VramReadTail = value.VramReadTail,
            LiveTextureSources = value.LiveTextureSources,
            TextureRegistrations = value.TextureRegistrations,
            TextureFileContext = value.TextureFileContext,
            SceneCopies = value.SceneCopies,
            PresentationTextures = banks,
            SharedPresentationTextureMembers = members.Count == 0 ? null : members.ToImmutable()
        }, options);
    }
}
