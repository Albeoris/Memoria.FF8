namespace Memoria.FF8.Runtime;

public abstract record NativeBattleGatlingGun305Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleGatlingGun305BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305RingSequence(byte Actor, byte Sequence) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305Sound(uint Value, int Pan) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305Contact(int Index) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305Stage(short Depth, uint ColorFlags) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, byte DepthShift = 2) : NativeBattleGatlingGun305Operation;
public sealed record NativeBattleGatlingGun305AnchorStep(NativeBattleGatlingGun305Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleGatlingGun305PointStep(NativeBattleGatlingGun305Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleGatlingGun305Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleGatlingGun305Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleGatlingGun305Frame, int, short, short, NativeBattleGatlingGun305PointStep> ReadBone502170, Func<NativeBattleGatlingGun305Frame, int, NativeBattleGatlingGun305AnchorStep> ReadAnchor571400, Func<NativeBattleGatlingGun305Frame, NativeBattleGatlingGun305Operation, NativeBattleGatlingGun305Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}
