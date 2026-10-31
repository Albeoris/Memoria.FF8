namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect314Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect314State State)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }

    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect314Step(NativeBattleEffect314Frame Frame, int Result);
public sealed record NativeBattleEffect314AddressStep(NativeBattleEffect314Frame Frame, uint Address);
public sealed record NativeBattleEffect314Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect314Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect314Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect314Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect314Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect314Frame, short> ReadCameraRoll, Func<NativeBattleEffect314Frame, uint, int, NativeBattleEffectArena> ReadArena, Func<NativeBattleEffect314Frame, uint, uint> ReadGeometryAddress, Func<NativeBattleEffect314Frame, uint, int, uint> ResolveBoneRecord, Func<NativeBattleEffect314Frame, int, NativeBattleEffect314AddressStep> AllocateTemporary, Func<NativeBattleEffect314Frame, int, NativeBattleEffect314Frame> ReleaseTemporary, Func<NativeBattleEffect314Frame, NativeBattleEffect314Operation, NativeBattleEffect314Frame> Execute)
{
    public Func<uint, long?>? PresentationResourceGeneration { get; init; }
    public Func<NativeBattleEffect314Frame, uint, int> ReadBoneCount { get; init; } = (_, _) => throw new NotSupportedException("Effect314 requires its current bone count.");
    public Func<NativeBattleEffect314Frame, int, NativeBattleEffect314IoStep> PollIo { get; init; } = (_, _) => throw new NotSupportedException("Effect314 requires its actual file queue.");
    public Func<NativeBattleEffect314Frame, uint, NativeBattleEffect298BodySource> ReadBody { get; init; } = (_, _) => throw new NotSupportedException("Effect314 requires its current body source and scratch allocation.");
}

public abstract record NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Prepare662C00(uint ModelAddress, uint BoneOffsetsAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314BonePoint502170(uint ModelAddress, int Bone, int Angle, uint DestinationAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314UploadTim505E30(uint SourceAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314WriteModel(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314WriteArena(uint Address, NativeBattleEffectArena Data, int Length) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314ClearGeometryMarkers6E5D20(uint ModelAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Shadow5088A0(uint ModelAddress, int OrderingBucket, int Count) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Prepare5095B0(uint ModelAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Restore508C90(uint ModelAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314PublishCursors(uint CommonCursor, uint PrivateCursor) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Blend56CC00(uint Source1Address, uint Source2Address, int Weight1, int Weight2, uint DestinationAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Stage5713E0(int Amount, uint Flags) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Music47E3C0(int Duration) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314StopVibration4A2940(int Handle) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect314Operation;

public sealed record NativeBattleEffect314IoStep(NativeBattleEffect314Frame Frame, bool Busy);
public sealed record NativeBattleEffect314InitializeIo534110(uint Address) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314QueueIo5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314AdvanceIo534210 : NativeBattleEffect314Operation;
public sealed record NativeBattleEffect314Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect314Operation;
