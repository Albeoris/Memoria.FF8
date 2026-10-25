namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect31Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect31State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect31Step(NativeBattleEffect31Frame Frame, int Result);
public sealed record NativeBattleEffect31PointStep(NativeBattleEffect31Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect31StackReadStep(NativeBattleEffect31Frame Frame, NativeBattlePackedMatrix32 Output);

public sealed record NativeBattleEffect31Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect31Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect31Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect31Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect31Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect31Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect31Frame, short> ReadCameraRoll, Func<NativeBattleEffect31Frame, uint, int, NativeBattleEffectArena> ReadPhysical, Func<NativeBattleEffect31Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect31PointStep> Point502170, Func<NativeBattleEffect31Frame, NativeBattleEffect31Operation, NativeBattleEffect31Frame> Execute)
{
    public Func<NativeBattleEffect31Frame, uint, uint, NativeBattleEffect31StackReadStep>? ReadActorStack { get; init; }
    public Func<NativeBattleEffect31Frame, uint, uint, uint, NativeBattlePackedMatrix32, NativeBattleEffect31Frame>? ObserveActorStack { get; init; }
}

public abstract record NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31UploadTim505E30(uint Source) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31RegisterCamera5099A0(uint Source) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31Sound501330(uint Source, uint Flags, byte Pan) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31StageTransition(int Group, ushort Intensity, bool ClearRgb) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31Contact506690(int AbsoluteIndex) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect31Operation;
public sealed record NativeBattleEffect31DrawOverlay(uint Color) : NativeBattleEffect31Operation;
