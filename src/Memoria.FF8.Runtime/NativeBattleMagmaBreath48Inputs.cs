namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMagmaBreath48Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMagmaBreath48State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleMagmaBreath48PointStep(NativeBattleMagmaBreath48Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleMagmaBreath48Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleMagmaBreath48Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleMagmaBreath48Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleMagmaBreath48Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleMagmaBreath48Frame, int, int, int, NativeBattleMagmaBreath48PointStep> ReadBone502170, Func<NativeBattleMagmaBreath48Frame, short> ReadCameraRoll, Func<NativeBattleMagmaBreath48Frame, NativeBattleMagmaBreath48Operation, NativeBattleMagmaBreath48Frame> Execute);

public abstract record NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48UpdateCaster8DC740(uint TaskAddress) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48UpdateTargetBounds8DC610_8DC870(uint TaskAddress) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48Contact506690(int ContactIndex) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48StageColorDepth(int Group, short Depth) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48StageRgb(int Group, byte R, byte G, byte B) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48DrawMesh88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMagmaBreath48Operation;
public sealed record NativeBattleMagmaBreath48SubmitPackets(NativeBattleModulePacketBatch Batch) : NativeBattleMagmaBreath48Operation;
