using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleActorDirectorResources(ushort Effect, NativeBattleEffectHeapAllocation Animation, uint Sound, NativeBattleCuragaExecutableSource NodeSource)
{
    public void Validate()
    {
        NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(Effect);
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != profile.CameraLength || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != profile.CameraLength || Sound != profile.Sound || NodeSource is null || NodeSource.ResourceAddress != profile.SourceAddress || NodeSource.Data.IsDefault || NodeSource.Data.Length != profile.SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(NodeSource.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("The actor director resources must retain their original module, camera, sound and uninitialized source bank.");
        }
    }
}

public sealed record NativeBattleActorDirectorFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleActorDirectorState State)
{
    public NativeBattleActorDirectorProfile Profile => NativeBattleActorDirectorProfile.ForEffect(Effect.Binding.Effect);
    public NativeBattleActorDirectorFrame WithState(NativeBattleActorDirectorState state)
    {
        state.Validate();
        if (state.Effect != Profile.Effect)
        {
            throw new InvalidDataException("The actor director state belongs to another original module.");
        }
        return this with { State = state };
    }
}

public abstract record NativeBattleActorDirectorOperation;
public sealed record NativeBattleActorDirectorContact(int Index) : NativeBattleActorDirectorOperation;
public sealed record NativeBattleActorDirectorSound(NativeBattleModuleSound Sound) : NativeBattleActorDirectorOperation;
public sealed record NativeBattleActorDirectorStage(short Depth, bool ClearRgb) : NativeBattleActorDirectorOperation;
public sealed record NativeBattleActorDirectorGenericTask(uint Address) : NativeBattleActorDirectorOperation;
public sealed record NativeBattleActorDirectorLeafStep(NativeBattleActorDirectorFrame Frame, int Result);
public sealed record NativeBattleActorDirectorInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleActorDirectorFrame, NativeBattleActorDirectorOperation, NativeBattleActorDirectorFrame> Execute, Func<NativeBattleActorDirectorFrame, uint, NativeBattleActorDirectorLeafStep> AdvanceGeneric)
{
    public ushort? CopiedCameraPadding { get; init; }
    public NativeBattlePackedMatrix32? PackedCamera { get; init; }
}
