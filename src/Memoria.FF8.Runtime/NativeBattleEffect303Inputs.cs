namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect303Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect303State State)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }

    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect303PointStep(NativeBattleEffect303Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect303VibrationStep(NativeBattleEffect303Frame Frame, int Handle);
public sealed record NativeBattleEffect303IoPoll(NativeBattleEffect303Frame Frame, bool Busy);
public sealed record NativeBattleEffect303Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect303Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect303Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect303Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect303Frame, uint, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleEffect303Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleEffect303Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect303Frame, NativeBattleEffect303Operation, NativeBattleEffect303Frame> Execute, Func<NativeBattleEffect303Frame, NativeBattleEffect303VibrationStep> StartVibration, Func<NativeBattleEffect303Frame, int, NativeBattleEffect303IoPoll> PollIo5342C0);

public abstract record NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303CopyPackedPosition(uint SourceModel, uint TargetModel) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303SelectSequence505C00(uint ModelAddress, ushort Sequence) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303EnterSequence508F90(uint ModelAddress) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303Activate487670(uint ModelAddress) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303ModelFields(uint ModelAddress, ushort ClearFlags0, ushort SetFlags0) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303RestorePose508C90(uint ModelAddress) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303DrawBody188(uint ModelAddress, NativeBattleEffectArena TextureRectangle8, ushort Clut, int VOffset, int UOffset, NativeBattleEffectArena Color4) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303StopVibration4A2940(int Handle) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303InitializeIo534110(int ArenaOffset) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303QueueFile5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303AdvanceIo534210 : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303DrawStaticMesh88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect303Operation;
public sealed record NativeBattleEffect303DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect303Operation;
