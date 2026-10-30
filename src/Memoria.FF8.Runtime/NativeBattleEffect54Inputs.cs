namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect54Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect54State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect54Step(NativeBattleEffect54Frame Frame, int Result);
public sealed record NativeBattleEffect54PointStep(NativeBattleEffect54Frame Frame, NativeBattleEffectArena Output8);

public sealed record NativeBattleEffect54Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect54Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect54Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect54Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect54Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect54Frame, short> ReadCameraRoll, Func<NativeBattleEffect54Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect54PointStep> Point502170, Func<NativeBattleEffect54Frame, NativeBattleEffect54Operation, NativeBattleEffect54Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54UploadTim505E30(uint Source) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54RegisterCamera5099A0(uint Source) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54Sound501330(uint Source, uint Flags, byte Pan) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54StageTransition(int Group, ushort Intensity, bool ClearRgb) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54Contact506690(int AbsoluteIndex) : NativeBattleEffect54Operation;
public sealed record NativeBattleEffect54DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect54Operation;
