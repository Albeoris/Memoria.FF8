using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAquaResources(ushort Effect, NativeBattleEffectHeapAllocation Animation, ImmutableDictionary<uint, ImmutableArray<byte>> Sprites, ImmutableArray<byte> WaveVertices, ImmutableArray<byte> WaveMesh, uint Sound, ImmutableArray<byte>? CasterDat72)
{
    public void Validate()
    {
        NativeBattleAquaProfile profile = NativeBattleAquaProfile.ForEffect(Effect);
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != (Effect == 55 ? 308 : 416) || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != Animation.Data.Length || Sprites is null || Sprites.Count != 6 || WaveVertices.IsDefault || WaveVertices.Length != 2504 || WaveMesh.IsDefault || WaveMesh.Length != 10600 || Sound != (Effect == 55 ? 0x64224u : 0x5577Eu))
        {
            throw new InvalidDataException("The Aqua module requires its complete original camera, mesh, vertex and sound resources.");
        }
        foreach (uint canonical in new uint[] { 0x15A38DC, 0x15A3A3C, 0x15A3B9C, 0x15A3CFC, 0x15A3E5C, 0x15A4008 })
        {
            int frames = canonical == 0x15A3E5C ? 16 : 13;
            if (!Sprites.TryGetValue(profile.SpriteAddress(canonical), out ImmutableArray<byte> sprite) || sprite.IsDefault || sprite.Length != (frames == 16 ? 428 : 352) || BinaryPrimitives.ReadUInt16LittleEndian(sprite.AsSpan().Slice(8)) != frames)
            {
                throw new InvalidDataException("The Aqua sprite directory belongs to another module or lacks its complete frames.");
            }
        }
        if (Effect == 72 ? CasterDat72 is not { IsDefault: false, Length: 7028 } : CasterDat72 is not null)
        {
            throw new InvalidDataException("The Aqua caster animation resource must retain its actual Blue Magic ownership.");
        }
    }
}
public sealed record NativeBattleAquaFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random)
{
    public NativeBattleAquaProfile Profile => NativeBattleAquaProfile.ForEffect(Effect.Binding.Effect);
    public NativeBattleAquaState State => (Profile.Effect == 55 ? Memory.Aqua55 : Memory.Aqua72) ?? throw new NotSupportedException("The Aqua effect requires its own retained module state.");

    public NativeBattleAquaFrame WithState(NativeBattleAquaState state)
    {
        state.Validate();
        if (state.Effect != Profile.Effect)
        {
            throw new InvalidDataException("The Aqua state belongs to another module.");
        }
        return this with { Memory = Profile.Effect == 55 ? Memory with { Aqua55 = state } : Memory with { Aqua72 = state } };
    }
}

public abstract record NativeBattleAquaOperation;
public sealed record NativeBattleAquaPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaContact(int Index) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaSound(NativeBattleModuleSound Sound) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaStage(short Depth, bool ClearColor) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaBindCaster(int Actor) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaSequence(int Actor, byte Sequence) : NativeBattleAquaOperation;
public sealed record NativeBattleAquaInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<int, short, int, NativeBattleModuleAnchor> ReadAnchor502170, Func<NativeBattleAquaFrame, NativeBattleAquaOperation, NativeBattleAquaFrame> Execute)
{
    public ushort? CameraPadding18 { get; init; }
    public NativeBattlePackedMatrix32? PackedCamera { get; init; }
}
