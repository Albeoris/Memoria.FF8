namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect283Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffect283State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect283PointStep(NativeBattleEffect283Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect283BodyResult(NativeBattleEffect283Frame Frame, short MaxDeviation);
public sealed record NativeBattleEffect283Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect283Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect283Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect283Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect283Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect283Frame, uint, NativeBattleEffect283PointStep> ReadAnchor571400, Func<NativeBattleEffect283Frame, NativeBattleEffect283Operation, NativeBattleEffect283Frame> Execute, Func<NativeBattleEffect283Frame, uint, short, short, short, int, int, NativeBattleEffect283BodyResult> DrawBody124, Func<NativeBattleEffect283Frame, NativeBattleGfAnimationSource, NativeBattleGfAnimationNode, NativeBattleEffectArena, NativeBattleEffect283Frame> DrawProgrammeNode);

public abstract record NativeBattleEffect283Operation;
public sealed record NativeBattleEffect283ModelFlags(uint ModelAddress, ushort ClearFlags0, ushort SetFlags0) : NativeBattleEffect283Operation;
public sealed record NativeBattleEffect283ModelColor44(uint ModelAddress, uint Value) : NativeBattleEffect283Operation;
public sealed record NativeBattleEffect283Stage5713E0(int Value, uint Flags) : NativeBattleEffect283Operation;
public sealed record NativeBattleEffect283Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect283Operation;
public sealed record NativeBattleEffect283DrawWindow108(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect283Operation;
public sealed record NativeBattleEffect283DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect283Operation;
