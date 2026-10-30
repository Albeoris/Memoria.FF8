namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect251Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect251State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect251Step(NativeBattleEffect251Frame Frame, int Result);
public sealed record NativeBattleEffect251PointStep(NativeBattleEffect251Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect251PollStep(NativeBattleEffect251Frame Frame, bool Busy);

public sealed partial record NativeBattleEffect251Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect251Frame, uint, int, NativeBattleEffectArena> ReadPhysical, Func<NativeBattleEffect251Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect251Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect251PointStep> Point502170, Func<NativeBattleEffect251Frame, uint, NativeBattleEffectArena, NativeBattleEffect251PointStep> Point571400, Func<NativeBattleEffect251Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect251Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect251Frame, short> ReadCameraRoll, Func<NativeBattleEffect251Frame, NativeBattleEffect251PollStep> IsIoBusy534270, Func<NativeBattleEffect251Frame, NativeBattleEffect251Operation, NativeBattleEffect251Frame> Execute);

public abstract record NativeBattleEffect251Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect251UploadTim505E30(uint SourceAddress) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251InitializeIo534110(uint Address) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251QueueFile5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251AdvanceIo534210 : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251Intensity5713E0(int Kind, int Strength) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251SetModelAnimation505C00(uint ModelAddress, short Mode) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251Contact506690(uint TargetArgumentAddress) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251DrawTarget6A2F60(uint ModelAddress, NativeBattleEffectArena Angles8, NativeBattleEffectArena Pivot8) : NativeBattleEffect251Operation;
public sealed record NativeBattleEffect251DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal = NativeBattleEffect251Profile.CursorGlobal, ushort Flags = 0) : NativeBattleEffect251Operation;
