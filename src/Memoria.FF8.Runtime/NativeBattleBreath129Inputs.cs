using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreath129Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBreath129State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleBreath129PointStep(NativeBattleBreath129Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleBreath129ActContext(NativeBattlePackedMatrix32 Matrix, int DepthBias, uint MorphAddress);
public sealed record NativeBattleBreath129ActStep(NativeBattleBreath129Frame Frame, int Remaining);
public sealed record NativeBattleBreath129BodySource(uint BodyRowAddress, uint GeometryAddress, Func<uint, int, NativeBattleEffectArena> ReadGeometry, ImmutableArray<NativeBattlePackedMatrix32> BoneMatrices);
public sealed record NativeBattleBreath129BodyStep(NativeBattleBreath129Frame Frame, NativeBattleBreath129BodySource Source);
public sealed record NativeBattleBreath129Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleBreath129Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleBreath129Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleBreath129Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleBreath129Frame, uint, short> ReadHeading, Func<NativeBattleBreath129Frame, uint, int, short, NativeBattleBreath129PointStep> ReadBone502170, Func<NativeBattleBreath129Frame, uint, NativeBattleBreath129PointStep> ReadAnchor571400, Func<NativeBattleBreath129Frame, uint, NativeBattleBreath129BodyStep> ReadBody, Func<NativeBattleBreath129Frame, uint, NativeBattleBreath129BodyStep> PublishBody508C90, Func<NativeBattleBreath129Frame, uint, NativeBattleGfAnimationSource, NativeBattleBreath129ActContext, NativeBattleBreath129ActStep> AdvanceAct, Func<NativeBattleBreath129Frame, NativeBattleBreath129Operation, NativeBattleBreath129Frame> Execute)
{
    public bool CapturePresentation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public abstract record NativeBattleBreath129Operation;
public sealed record NativeBattleBreath129Sound(uint Packed, uint Flags, byte Pan) : NativeBattleBreath129Operation;
public sealed record NativeBattleBreath129ContactIndex(int Index) : NativeBattleBreath129Operation;
public sealed record NativeBattleBreath129ComposeBody5095B0(uint ActorAddress) : NativeBattleBreath129Operation;
public sealed record NativeBattleBreath129Stage5713E0(int Accumulator, int Mode) : NativeBattleBreath129Operation;
public sealed record NativeBattleBreath129DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleBreath129Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleBreath129DrawDefaultMesh88(uint TemporaryAddress, uint SourceAddress) : NativeBattleBreath129Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleBreath129DeliverBatch(NativeBattleGfGeometryBatch Batch) : NativeBattleBreath129Operation;
public sealed record NativeBattleBreath129AppendPacket(uint Address, int Allocation, int Written, int Bucket, ushort Flags, byte[] Data, bool[] Known) : NativeBattleBreath129Operation
{
    public bool Insert { get; init; } = true;
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualGeometry? PilotGeometry { get; init; }
    public NativeBattleGfPacketByteState[] WriteMask { get; init; } = [];
    public ImmutableArray<int> VertexPayload4 { get; init; } = [];
}
