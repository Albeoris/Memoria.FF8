namespace Memoria.FF8.Runtime;

public sealed record NativeBattleJunk44Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleJunk44State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleJunk44PointStep(NativeBattleJunk44Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleJunk44IoStep(NativeBattleJunk44Frame Frame, bool Complete);
public sealed record NativeBattleJunk44Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleJunk44Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleJunk44Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleJunk44Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleJunk44Frame, uint, NativeBattleJunk44PointStep> ReadAnchor571400, Func<NativeBattleJunk44Frame, uint, int, int, NativeBattleJunk44PointStep> ReadBone502170, Func<NativeBattleJunk44Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleJunk44Frame, NativeBattleJunk44IoStep> ReloadComplete, Func<NativeBattleJunk44Frame, NativeBattleJunk44Operation, NativeBattleJunk44Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44Contact506690(int ContactIndex) : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44SelectSequence505C00(uint ModelAddress, int Sequence) : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44ModelPositionY(uint ModelAddress, short Y) : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44RestoreModelPosition(uint ModelAddress, NativeBattleEffectArena Point8) : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44CameraWord1D97712(ushort Value) : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44AdvanceIo534210 : NativeBattleJunk44Operation;
public sealed record NativeBattleJunk44DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleJunk44Operation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleJunk44DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleJunk44Operation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
