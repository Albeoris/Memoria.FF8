namespace Memoria.FF8.Runtime;

public abstract record NativeBattleAcid317Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleAcid317BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317RingSequence(byte Actor, byte Sequence) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317SnapshotBody(byte Actor, uint CacheAddress) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317AdvanceAct(uint TaskAddress) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317DrawBody132(byte Actor, short Fraction) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317ProjectPoints(uint HeadAddress, short Count) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317DrawTrail(uint HeadAddress, short Count) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317ActorFlags(byte Actor, ushort ClearMask, ushort SetMask) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317Sound(uint Value, int Pan) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317Contact(int Index) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, byte DepthShift = 2) : NativeBattleAcid317Operation;
public sealed record NativeBattleAcid317PointStep(NativeBattleAcid317Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleAcid317AnchorStep(NativeBattleAcid317Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleAcid317Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, int> ReadBoneCount, Func<NativeBattleAcid317Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleAcid317Frame, int, NativeBattleAcid317AnchorStep> ReadAnchor571400, Func<NativeBattleAcid317Frame, int, short, short, NativeBattleAcid317PointStep> ReadBone502170, Func<NativeBattleAcid317Frame, NativeBattleAcid317PointStep> ReadMouth, Func<NativeBattleAcid317Frame, NativeBattleAcid317Operation, NativeBattleAcid317Frame> Execute)
{
    public NativeBattleSummonColors? Colors { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}
