using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Retained local storage of the160 module, separate from its physical task banks.</summary>
public sealed record NativeBattleRenzokuken160State([property: JsonRequired] byte Actor, [property: JsonRequired] int HeaderIndex, [property: JsonRequired] int FirstTarget, [property: JsonRequired] bool InitialControl, [property: JsonRequired] int Duration, [property: JsonRequired] ImmutableArray<byte> Sparks, [property: JsonRequired] ImmutableArray<byte> Debris, [property: JsonRequired] ImmutableArray<byte> Spirals, [property: JsonRequired] ImmutableArray<byte> TrailHistory, [property: JsonRequired] ImmutableArray<byte> ImpactStates, [property: JsonRequired] ImmutableArray<short> TrailInner, [property: JsonRequired] ImmutableArray<short> TrailOuter, [property: JsonRequired] NativeBattleVector Origin, [property: JsonRequired] NativeBattleVector Direction, [property: JsonRequired] int DrawParity)
{
    [JsonRequired]
    public ushort OriginPadding { get; init; }
    [JsonRequired]
    public uint DirectionPadding { get; init; }
    [JsonRequired]
    public ushort TrailHistoryUnknownPaddingMask { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? RetainedResources { get; init; }
    [JsonRequired]
    public ImmutableArray<byte> AbSegments { get; init; } = [];

    public static NativeBattleRenzokuken160State FromColdProcess() => new(0, 0, 0, false, 0, new byte[40 * 12].ToImmutableArray(), new byte[40 * 20].ToImmutableArray(), new byte[60 * 16].ToImmutableArray(), new byte[7 * 20].ToImmutableArray(), new byte[12 * 156].ToImmutableArray(), new short[32 * 4].ToImmutableArray(), new short[32 * 4].ToImmutableArray(), new(0, 0, 0), new(0, 0, 0), 0);

    public void Validate()
    {
        if (Actor >= 7 || HeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || FirstTarget is < 0 or > 6 || Sparks.IsDefault || Sparks.Length != 480 || Debris.IsDefault || Debris.Length != 800 || Spirals.IsDefault || Spirals.Length != 960 || TrailHistory.IsDefault || TrailHistory.Length != 140 || ImpactStates.IsDefault || ImpactStates.Length != 1872 || TrailInner.IsDefault || TrailInner.Length != 128 || TrailOuter.IsDefault || TrailOuter.Length != 128 || DrawParity is < 0 or > 1)
        {
            throw new InvalidDataException("Native160 lost its physical particle, trail or retained action owners.");
        }
    }

    public NativeBattleRenzokuken160State Enter(byte actor, int headerIndex, int firstTarget, bool initialControl)
    {
        Validate();
        NativeBattleRenzokuken160State entered = this with { Actor = actor, HeaderIndex = headerIndex, FirstTarget = firstTarget, InitialControl = initialControl, Duration = 4, AbSegments = [], Sparks = ClearMarkers(Sparks, 12), Debris = ClearMarkers(Debris, 20), Spirals = ClearMarkers(Spirals, 16) };
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
    public NativeBattleRenzokuken160State? Renzokuken160
    {
        get => effectState.Renzokuken160;
        init => effectState = effectState with { Renzokuken160 = value };
    }
}

public sealed record NativeBattleRenzokuken160Resources(NativeBattleEffectHeapAllocation Animation, BattleNativeRenzokukenResource ActorResource, ImmutableArray<NativeBattleSearchSpriteResource> ParticleSprites, ImmutableArray<uint> SpiralColors, ImmutableArray<int> ContactLeadFrames, ImmutableArray<NativeBattleStaticMeshResource> Meshes, uint AppearanceSound, uint DefaultModelColor)
{
    public ImmutableArray<int> SpecialAlignmentAxis { get; init; } = [];
    public ImmutableArray<NativeBattleSearchSpriteResource> SpecialSprites { get; init; } = [];
    public ImmutableArray<int> ImpactAxis { get; init; } = [];
    public ImmutableArray<ushort> StreakAgeOffsets { get; init; } = [];
    public ImmutableArray<ushort> StreakPaletteOffsets { get; init; } = [];
    public NativeBattleVector StreakReferencePoint { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleVector>? ContactOffsets { get; init; }
}
public sealed record NativeBattleRenzokuken160Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleRenzokuken160Sprite(NativeBattleSearchSpriteResource Resource, ushort Frame, NativeBattleVector Position, short Scale, uint? Color, ushort Flags);
public sealed record NativeBattleRenzokuken160SpriteStep(NativeBattleRenzokuken160Frame Frame, bool AnimationEnded);
public sealed record NativeBattleRenzokuken160ChildStep(NativeBattleRenzokuken160Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
