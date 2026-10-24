namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect323Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect323State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect323Step(NativeBattleEffect323Frame Frame, int Result);
public sealed record NativeBattleEffect323PointStep(NativeBattleEffect323Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect323Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect323Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect323Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect323Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect323Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect323Frame, short> ReadCameraRoll, Func<NativeBattleEffect323Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect323PointStep> Point502170, Func<NativeBattleEffect323Frame, uint, NativeBattleEffectArena, NativeBattleEffect323PointStep> Anchor571400, Func<NativeBattleEffect323Frame, NativeBattleEffectArena, int, int, NativeBattleEffectArena, NativeBattleEffect323PointStep> SampleTrajectory, Func<NativeBattleEffect323Frame, NativeBattleEffect323Operation, NativeBattleEffect323Frame> Execute);

public abstract record NativeBattleEffect323Operation;
public sealed record NativeBattleEffect323UploadTim505E30(uint SourceAddress) : NativeBattleEffect323Operation;
public sealed record NativeBattleEffect323RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect323Operation;
public sealed record NativeBattleEffect323MeshMask(uint ModelAddress, uint AndMask, uint OrMask) : NativeBattleEffect323Operation;
public sealed record NativeBattleEffect323Contact506690(int ContactIndex) : NativeBattleEffect323Operation;
public sealed record NativeBattleEffect323Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect323Operation;
public sealed record NativeBattleEffect323DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect323Operation;
