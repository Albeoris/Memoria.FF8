namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect345Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect345State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect345Step(NativeBattleEffect345Frame Frame, int Result);
public sealed record NativeBattleEffect345PointStep(NativeBattleEffect345Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect345OwnedModelStep(NativeBattleEffect345Frame Frame, int Result);

public sealed record NativeBattleEffect345Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect345Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect345Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect345Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect345Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect345Frame, short> ReadCameraRoll, Func<NativeBattleEffect345Frame, NativeBattleStageColors> ReadModelColors, Func<NativeBattleEffect345Frame, uint, int, NativeBattleEffectArena> ReadPhysical, Func<NativeBattleEffect345Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect345PointStep> Point502170, Func<NativeBattleEffect345Frame, uint, NativeBattleEffectOwnedModelResource, ushort, NativeBattleEffect345OwnedModelStep> InitializeOwnedModel8DD0F0, Func<NativeBattleEffect345Frame, uint, short, NativeBattleEffect345OwnedModelStep> SelectOwnedModel8DD190, Func<NativeBattleEffect345Frame, uint, NativeBattleEffect345OwnedModelStep> AdvanceOwnedModel8DD220, Func<NativeBattleEffect345Frame, NativeBattleEffectOwnedModelRenderInputs> ReadModelRenderInputs, Func<NativeBattleEffect345Frame, NativeBattleEffect345Operation, NativeBattleEffect345Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }

    public required Func<NativeBattleEffect345Frame, int, NativeBattleGraphicsModel> ReadModel { get; init; }
    public required Func<NativeBattleEffect345Frame, int, NativeBattlePoseSource> ReadPoseSource { get; init; }
}

public abstract record NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345UploadTim505E30(uint Source) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345RegisterCamera5099A0(uint Source) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345ReleaseSource508630(uint Source, uint CompletionAddress) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345Sound501330(uint Source, uint Flags, byte Pan) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345StageTransition(int Group, ushort Intensity, bool ClearRgb) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345Contact506690(int AbsoluteIndex) : NativeBattleEffect345Operation;
public sealed record NativeBattleEffect345DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect345Operation;
