namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect213Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect213State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect213Step(NativeBattleEffect213Frame Frame, int Result);
public sealed record NativeBattleEffect213PointStep(NativeBattleEffect213Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect213CountStep(NativeBattleEffect213Frame Frame, int Count);
public sealed record NativeBattleEffect213Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect213Frame, uint> ReadFlags, Func<NativeBattleEffect213Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect213Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect213Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect213Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect213Frame, short> ReadCameraRoll, Func<NativeBattleEffect213Frame, uint, short, short, NativeBattleEffectArena, NativeBattleEffect213PointStep> Point502170, Func<NativeBattleEffect213Frame, uint, NativeBattleEffect213CountStep> CountVertices69EE80, Func<NativeBattleEffect213Frame, uint, int, NativeBattleEffectArena, NativeBattleEffect213PointStep> PoseVertex6A06C0, Func<NativeBattleEffect213Frame, NativeBattleEffect213Operation, NativeBattleEffect213Frame> Execute);

public abstract record NativeBattleEffect213Operation;
public sealed record NativeBattleEffect213UploadTim505E30(uint SourceAddress) : NativeBattleEffect213Operation;
public sealed record NativeBattleEffect213RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect213Operation;
public sealed record NativeBattleEffect213Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect213Operation;
public sealed record NativeBattleEffect213PositionalSound5013A0(uint SourceAddress, uint Flags, NativeBattleEffectArena Point8) : NativeBattleEffect213Operation;
public sealed record NativeBattleEffect213Contact506690(int OriginalIndex) : NativeBattleEffect213Operation;
public sealed record NativeBattleEffect213DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect213Operation;
