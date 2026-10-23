namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect315Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect315State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect315PointStep(NativeBattleEffect315Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect315VibrationStep(NativeBattleEffect315Frame Frame, int Handle);
public sealed record NativeBattleEffect315Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect315Frame, uint> ReadFlags, Func<NativeBattleEffect315Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect315Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect315Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect315Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect315Frame, uint, byte, short, NativeBattleEffectArena, NativeBattleEffect315PointStep> ReadAnchor502170, Func<NativeBattleEffect315Frame, uint> ReadTransitionColor40, Func<NativeBattleEffect315Frame, NativeBattleEffect315Operation, NativeBattleEffect315Frame> Execute, Func<NativeBattleEffect315Frame, NativeBattleEffect315VibrationStep> StartVibration, Func<NativeBattleEffect315Frame, int, bool> IsPlayingVibration);

public abstract record NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ModelFlags(uint ModelAddress, ushort ClearFlags0, ushort SetFlags0) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ModelFlags8(uint ModelAddress, uint ClearFlags8, uint SetFlags8) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ModelInitial20XZ(uint ModelAddress, short X, short Z) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ModelPosition28XZ(uint ModelAddress, short X, short Z) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ModelDefaultSequence2(uint ModelAddress, byte Value) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ModelColor40(uint ModelAddress, uint Value) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315Activate487670(int ActorIndex) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315Contact506690 : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315Sound5013A0(uint Packed, uint Flags, NativeBattleEffectArena Point8) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315StopVibration4A2940(int Handle) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315ReleaseCommon508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315DrawBody156(uint TemporaryAddress, uint ModelAddress) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315DrawSprite180(uint TemporaryAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315DrawMesh88(uint TemporaryAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315DrawScrolling124(uint TemporaryAddress, int TableBaseBucket = 17, int DepthShift = 3) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315DrawMesh96(uint TemporaryAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect315Operation;
public sealed record NativeBattleEffect315DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect315Operation;
