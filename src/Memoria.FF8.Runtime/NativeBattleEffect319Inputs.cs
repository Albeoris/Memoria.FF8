namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect319Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect319State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect319Step(NativeBattleEffect319Frame Frame, int Result);
public sealed record NativeBattleEffect319PointStep(NativeBattleEffect319Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect319PollStep(NativeBattleEffect319Frame Frame, bool Complete);
public sealed record NativeBattleEffect319Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect319Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect319Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect319Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect319Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect319Frame, short> ReadCameraRoll, Func<NativeBattleEffect319Frame, uint, short, short, NativeBattleEffectArena, NativeBattleEffect319PointStep> Point502170, Func<NativeBattleEffect319Frame, uint, NativeBattleEffectArena, NativeBattleEffect319PointStep> Anchor571400, Func<NativeBattleEffect319Frame, uint, int, uint> ResolveBoneRecord, Func<NativeBattleEffect319Frame, int, NativeBattleEffect319PollStep> IoSelect5342C0, Func<NativeBattleEffect319Frame, NativeBattleEffect319Operation, NativeBattleEffect319Frame> Execute);

public abstract record NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319UploadTim505E30(uint SourceAddress) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319MeshMask(uint ModelAddress, uint AndMask, uint OrMask) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319ScaleBone56BEF0(uint BoneRecordAddress, short XY, short Z) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319Contact506690(int OriginalIndex) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319Initialize534110(uint Address) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319Advance534210 : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319Queue5341D0(short FileId, uint Destination, short Type) : NativeBattleEffect319Operation;
public sealed record NativeBattleEffect319DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect319Operation;
