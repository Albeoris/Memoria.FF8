namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect320Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect320State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect320Step(NativeBattleEffect320Frame Frame, int Result);
public sealed record NativeBattleEffect320PointStep(NativeBattleEffect320Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect320Inputs(
    NativeBattleSummonColors Colors,
    Func<NativeBattleEffect320Frame, NativeBattleActionPacketsState> ReadPackets,
    Func<NativeBattleEffect320Frame, int, NativeBattleGraphicsModel> ReadModel,
    Func<NativeBattleEffect320Frame, int, NativeBattleEffectArena> ReadModelArena9C,
    Func<NativeBattleEffect320Frame, NativeBattlePackedMatrix32> ReadView,
    Func<NativeBattleEffect320Frame, NativeBattleCameraProjection> ReadProjection,
    Func<NativeBattleEffect320Frame, short> ReadCameraRoll,
    Func<NativeBattleEffect320Frame, int, NativeBattleEffectArena, NativeBattleEffect320PointStep> Anchor571400,
    Func<NativeBattleEffect320Frame, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect320PointStep> Origin65DA30,
    Func<NativeBattleEffect320Frame, NativeBattleEffect320Operation, NativeBattleEffect320Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect320Operation;
public sealed record NativeBattleEffect320UploadTim505E30(uint SourceAddress) : NativeBattleEffect320Operation;
public sealed record NativeBattleEffect320RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect320Operation;
public sealed record NativeBattleEffect320Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect320Operation;
public sealed record NativeBattleEffect320AllContacts506BA0(int FirstContactIndex, int Count) : NativeBattleEffect320Operation;
public sealed record NativeBattleEffect320DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect320Operation;
