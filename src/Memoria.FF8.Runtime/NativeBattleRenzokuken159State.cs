using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained local storage of the159 module, separate from its physical task banks.</summary>
public sealed record NativeBattleRenzokuken159State([property: JsonRequired] byte Actor, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] int FirstTarget, [property: JsonRequired] bool InitialControl, [property: JsonRequired] int Duration, [property: JsonRequired] ImmutableArray<byte> Sparks, [property: JsonRequired] ImmutableArray<byte> Debris, [property: JsonRequired] ImmutableArray<byte> Spirals, [property: JsonRequired] ImmutableArray<byte> TrailHistory, [property: JsonRequired] ImmutableArray<byte> ImpactStates, [property: JsonRequired] ImmutableArray<short> TrailInner, [property: JsonRequired] ImmutableArray<short> TrailOuter, [property: JsonRequired] NativeBattleVector Origin, [property: JsonRequired] NativeBattleVector Direction, [property: JsonRequired] int DrawParity)
{
    [JsonRequired]
    public ushort OriginPadding { get; init; }
    [JsonRequired]
    public uint DirectionPadding { get; init; }
    [JsonRequired]
    public ushort TrailHistoryUnknownPaddingMask { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken159Resources? RetainedResources { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> AbSegments { get; init; } = [];

    public static NativeBattleRenzokuken159State FromColdProcess() => new(0, 0, 0, false, 0, new byte[40 * 12].ToImmutableArray(), new byte[80 * 20].ToImmutableArray(), new byte[60 * 16].ToImmutableArray(), new byte[7 * 20].ToImmutableArray(), new byte[12 * 156].ToImmutableArray(), new short[32 * 4].ToImmutableArray(), new short[32 * 4].ToImmutableArray(), new(0, 0, 0), new(0, 0, 0), 0);

    public void Validate()
    {
        if (Actor >= 7 || HeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || FirstTarget is < 0 or > 6 || Sparks.IsDefault || Sparks.Length != 480 || Debris.IsDefault || Debris.Length != 1600 || Spirals.IsDefault || Spirals.Length != 960 || TrailHistory.IsDefault || TrailHistory.Length != 140 || ImpactStates.IsDefault || ImpactStates.Length != 1872 || TrailInner.IsDefault || TrailInner.Length != 128 || TrailOuter.IsDefault || TrailOuter.Length != 128 || DrawParity is < 0 or > 1)
        {
            throw new InvalidDataException("Native159 lost its physical particle, trail or retained action owners.");
        }
    }

    public NativeBattleRenzokuken159State Enter(byte actor, int headerIndex, int firstTarget, bool initialControl)
    {
        Validate();
        NativeBattleRenzokuken159State entered = this with { Actor = actor, HeaderIndex = headerIndex, FirstTarget = firstTarget, InitialControl = initialControl, Duration = 4, AbSegments = [], Sparks = ClearMarkers(Sparks, 12), Debris = ClearMarkers(Debris, 20), Spirals = ClearMarkers(Spirals, 16) };
        entered.Validate();
        return entered;
    }

    private static ImmutableArray<byte> ClearMarkers(ImmutableArray<byte> bank, int stride)
    {
        byte[] bytes = bank.ToArray();
        for (int offset = 0; offset < bytes.Length; offset += stride)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), -1);
        }
        return bytes.ToImmutableArray();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken159State? Renzokuken159
    {
        get => effectState.Renzokuken159;
        init => effectState = effectState with { Renzokuken159 = value };
    }
}

public sealed record NativeBattleRenzokuken159Resources(NativeBattleEffectHeapAllocation Animation, BattleNativeRenzokukenResource ActorResource, ImmutableArray<NativeBattleSearchSpriteResource> ParticleSprites, ImmutableArray<uint> SpiralColors, ImmutableArray<int> ContactLeadFrames, ImmutableArray<NativeBattleStaticMeshResource> Meshes, uint AppearanceSound, uint DefaultModelColor)
{
    public ImmutableArray<int> ContactSpread { get; init; } = [];
    public NativeBattleGraphicsResource BindTarget(NativeBattlePoseSource original) => ActorResource.TargetResources[0].BindGraphicsResource(original);

    [JsonIgnore]
    public NativeBattleRenzokuken160Resources Shared => new(Animation, ActorResource, ParticleSprites, SpiralColors, ContactLeadFrames, Meshes, AppearanceSound, DefaultModelColor) { ImpactAxis = ImpactAxis, StreakAgeOffsets = StreakAgeOffsets, StreakPaletteOffsets = StreakPaletteOffsets, StreakReferencePoint = StreakReferencePoint };

    public ImmutableArray<int> ImpactAxis { get; init; } = [];
    public ImmutableArray<ushort> StreakAgeOffsets { get; init; } = [];
    public ImmutableArray<ushort> StreakPaletteOffsets { get; init; } = [];
    public NativeBattleVector StreakReferencePoint { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleVector>? ContactOffsets { get; init; }
}
public sealed record NativeBattleRenzokuken159Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleRenzokuken159Sprite(NativeBattleSearchSpriteResource Resource, ushort Frame, NativeBattleVector Position, short Scale, uint? Color, ushort Flags)
{
    [JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleRenzokuken159SpriteStep(NativeBattleRenzokuken159Frame Frame, bool AnimationEnded);
public sealed record NativeBattleRenzokuken159ChildStep(NativeBattleRenzokuken159Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
