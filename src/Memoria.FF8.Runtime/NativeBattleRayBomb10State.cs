using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRayBomb10Resources(NativeBattleEffectHeapAllocation Camera, ImmutableArray<NativeBattleStaticMeshResource> Meshes, ImmutableArray<NativeBattleModuleResource> Sprites, ImmutableArray<byte> DustOffsets, ImmutableArray<byte> DustDirections, ImmutableArray<byte> PuffX, ImmutableArray<byte> PuffZ, ImmutableArray<byte> DebrisX, ImmutableArray<byte> DebrisZ, uint MuzzleSound, uint BlastSound);
public sealed record NativeBattleRayBomb10State([property: JsonRequired] ImmutableArray<byte> Particles, [property: JsonRequired] ImmutableArray<byte?> SavedActors, [property: JsonRequired] NativeBattleModuleAnchor Muzzle, [property: JsonRequired] NativeBattleModuleAnchor Centre, [property: JsonRequired] uint Context, [property: JsonRequired] uint Caster, [property: JsonRequired] uint ArenaBase, [property: JsonRequired] uint PacketCursor)
{
    public static NativeBattleRayBomb10State FromColdProcess() => new(new byte[80 * 24].ToImmutableArray(), Enumerable.Repeat<byte?>(0, 3 * 20).ToImmutableArray(), new(0, 0, 0, 0), new(0, 0, 0, 0), 0, 0, 0, 0);

    public void Validate()
    {
        if (Particles.IsDefault || Particles.Length != 80 * 24 || SavedActors.IsDefault || SavedActors.Length != 3 * 20 || Muzzle is null || Centre is null)
        {
            throw new InvalidDataException("Native Ray-Bomb requires its retained80-particle and three-actor physical storage.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRayBomb10State? RayBomb10 { get; init; }
}

public sealed record NativeBattleRayBomb10Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Mesh(uint Resource, NativeBattleActionMatrix Matrix, bool CameraSpace, uint Flags, int? Fade, uint? FarColor) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Sprite(NativeBattleModuleDraw Draw) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10WorldSprite(uint Resource, short Phase, NativeBattleActionMatrix World, ushort Flags, uint Color) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Model(NativeBattleGraphicsModel Model) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Sequence(byte Actor, int Sequence) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Contact(int Index) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Flash(int Intensity) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Sound(uint Sound, uint Flags, NativeBattleModuleAnchor Position) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Restore(uint Destination, uint Completion) : NativeBattleRayBomb10Operation;
public sealed record NativeBattleRayBomb10Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleVector Eye, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattleActionMatrix> ReadMatrix, Func<int, byte, short, NativeBattleVector> ReadBone, Func<ushort, NativeBattleVector> ReadCentre, Func<int, NativeBattleModuleAnchor> ReadDefaultAnchor, Func<NativeBattleRayBomb10Frame, NativeBattleRayBomb10Operation, NativeBattleRayBomb10Frame> Execute);
