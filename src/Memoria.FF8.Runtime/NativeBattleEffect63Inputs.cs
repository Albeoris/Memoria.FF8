namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect63Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect63State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect63Step(NativeBattleEffect63Frame Frame, int Result);
public sealed record NativeBattleEffect63PointStep(NativeBattleEffect63Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect63Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect63Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect63Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect63Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect63Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect63Frame, short> ReadCameraRoll, Func<NativeBattleEffect63Frame, uint, NativeBattleEffectArena, NativeBattleEffect63PointStep> Anchor571400, Func<NativeBattleEffect63Frame, uint, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect63PointStep> Point6E7710, Func<NativeBattleEffect63Frame, NativeBattleEffect63Operation, NativeBattleEffect63Frame> Execute);

public abstract record NativeBattleEffect63Operation;
public sealed record NativeBattleEffect63UploadTim505E30(uint SourceAddress) : NativeBattleEffect63Operation;
public sealed record NativeBattleEffect63RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect63Operation;
public sealed record NativeBattleEffect63Stage5713E0(int Amount, uint Color) : NativeBattleEffect63Operation;
public sealed record NativeBattleEffect63Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect63Operation;
public sealed record NativeBattleEffect63Contact506690(int AbsoluteContactIndex) : NativeBattleEffect63Operation;
public sealed record NativeBattleEffect63DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect63Operation;
