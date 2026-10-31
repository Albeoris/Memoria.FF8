using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect298Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffect298State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect298PointStep(NativeBattleEffect298Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect298VibrationStep(NativeBattleEffect298Frame Frame, int Handle);
public sealed record NativeBattleEffect298IoPoll(NativeBattleEffect298Frame Frame, bool Busy);
public sealed record NativeBattleEffect298BodySource(NativeBattlePoseSource PoseSource, NativeBattleDecodedPose DecodedPose, ImmutableArray<NativeBattlePackedMatrix32> CurrentBones, uint GeometryAddress, Func<uint, int, NativeBattleEffectArena> ReadGeometry)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public long? PresentationResourceGeneration { get; init; }
    public uint? VertexScratchAddress { get; init; }
    public int? VertexScratchLength { get; init; }
}
public sealed record NativeBattleEffect298Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect298Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect298Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect298Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect298Frame, uint, int, int, NativeBattleEffect298PointStep> ReadBone502170, Func<NativeBattleEffect298Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect298Frame, uint, NativeBattleEffect298BodySource> ReadBody, Func<NativeBattleEffect298Frame, NativeBattleEffect298Operation, NativeBattleEffect298Frame> Execute, Func<NativeBattleEffect298Frame, NativeBattleEffect298VibrationStep> StartVibration, Func<NativeBattleEffect298Frame, int, NativeBattleEffect298IoPoll> PollIo5342C0)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298Music47E3C0(int Duration) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298StopVibration4A2940(int Handle) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298ModelFields(uint ModelAddress, ushort ClearFlags0, ushort SetFlags0, short? PositionZ = null) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298InitializeIo534110(int ArenaOffset) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298QueueFile5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298AdvanceIo534210 : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298ApplyOffsetPose662C00(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298RestorePose508C90(uint ModelAddress) : NativeBattleEffect298Operation;
public sealed record NativeBattleEffect298DrawOffsetBody672330(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect298Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect298DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect298Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect298DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect298Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect298DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect298Operation;
