using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRayBomb10Resources(NativeBattleEffectHeapAllocation Camera, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<byte> DustOffsets, ImmutableArray<byte> DustDirections, ImmutableArray<byte> PuffX, ImmutableArray<byte> PuffZ, ImmutableArray<byte> DebrisX, ImmutableArray<byte> DebrisZ, uint MuzzleSound, uint BlastSound);
public sealed record NativeBattleRayBomb10State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] NativeBattleModuleAnchor Muzzle, [property: JsonRequired] NativeBattleModuleAnchor Centre, [property: JsonRequired] uint Context, [property: JsonRequired] uint ArenaBase, [property: JsonRequired] uint PacketCursor)
{
    public static NativeBattleRayBomb10State FromColdProcess() => new(new byte[80 * 24].ToImmutableArray(), new(0, 0, 0, 0), new(0, 0, 0, 0), 0, 0, 0);

    public void Validate()
    {
        if (Particles.IsDefault || Particles.Length != 80 * 24 || Muzzle is null || Centre is null)
        {
            throw new InvalidDataException("Native Ray-Bomb requires its retained 80-particle storage.");
        }
    }
}

internal sealed record NativeBattleSavedActorTransform(ushort Flags0, short? Pitch, short? Yaw, short? Roll, ushort? RotationPadding18, NativeBattleVector Position, short Vertical34);

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    internal ImmutableDictionary<BattleModelOwner, NativeBattleSavedActorTransform> SavedActorTransforms { get; init; } = ImmutableDictionary<BattleModelOwner, NativeBattleSavedActorTransform>.Empty;
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRayBomb10State? RayBomb10
    {
        get => effectState.RayBomb10;
        init => effectState = effectState with { RayBomb10 = value };
    }
}

public sealed record NativeBattleRayBomb10Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleRayBomb10Operation
{
    [JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleRayBomb10Mesh(uint Resource, NativeBattleActionMatrix Matrix, bool CameraSpace, uint Flags, int? Fade, uint? FarColor) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Sprite(NativeBattleModuleDraw Draw) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10WorldSprite(uint Resource, short Phase, NativeBattleActionMatrix World, ushort Flags, uint Color) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Model(BattleModelOwner Actor, NativeBattleGraphicsModel Model) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Sequence(BattleModelOwner Actor, int Sequence) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Contact(BattleContactOccurrence Occurrence) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Flash(int Intensity) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Sound(uint Sound, uint Flags, NativeBattleModuleAnchor Position) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Restore(uint Destination, uint Completion) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleVector Eye, Func<BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<BattleModelOwner, NativeBattleActionMatrix> ReadMatrix, Func<BattleModelOwner, byte, short, NativeBattleVector> ReadBone, Func<BattleModelTargets, NativeBattleVector> ReadCentre, Func<BattleModelOwner, NativeBattleModuleAnchor> ReadDefaultAnchor, Func<NativeBattleRayBomb10Frame, NativeBattleRayBomb10Operation, NativeBattleRayBomb10Frame> Execute);
