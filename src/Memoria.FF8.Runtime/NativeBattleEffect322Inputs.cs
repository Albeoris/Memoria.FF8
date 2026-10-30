namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect322Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect322State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect322Step(NativeBattleEffect322Frame Frame, int Result);
public sealed record NativeBattleEffect322PointStep(NativeBattleEffect322Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect322IoCompletionStep(NativeBattleEffect322Frame Frame, bool Complete);
public sealed record NativeBattleEffect322Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect322Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect322Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect322Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect322Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect322Frame, short> ReadCameraRoll, Func<NativeBattleEffect322Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect322PointStep> Point502170, Func<NativeBattleEffect322Frame, uint, NativeBattleEffectArena, NativeBattleEffect322PointStep> Anchor571400, Func<NativeBattleEffect322Frame, NativeBattleEffectArena, int, int, NativeBattleEffectArena, NativeBattleEffect322PointStep> SampleTrajectory, Func<NativeBattleEffect322Frame, int, NativeBattleEffect322IoCompletionStep> Select5342C0, Func<NativeBattleEffect322Frame, NativeBattleEffect322Operation, NativeBattleEffect322Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322UploadTim505E30(uint SourceAddress) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322MeshMask(uint ModelAddress, uint AndMask, uint OrMask) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322Contact506690(int ContactIndex) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322IoInitialize534110(uint Address) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322IoQueue5341D0(short FileId, uint Destination, short Type) : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322IoAdvance534210 : NativeBattleEffect322Operation;
public sealed record NativeBattleEffect322DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect322Operation;
