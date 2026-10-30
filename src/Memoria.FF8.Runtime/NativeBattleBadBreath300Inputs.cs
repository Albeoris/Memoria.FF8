namespace Memoria.FF8.Runtime;

public abstract record NativeBattleBadBreath300Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleBadBreath300BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300RingSequence(byte Actor, byte Sequence) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300Sound(uint Value, int Pan) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300Contact(int Index) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300Clip(int Phase) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300DrawStrip(uint TemporaryAddress, uint SourceAddress, uint SelectedVerticesAddress) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300DrawParticle(uint TaskAddress) : NativeBattleBadBreath300Operation;
public sealed record NativeBattleBadBreath300AnchorStep(NativeBattleBadBreath300Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleBadBreath300PointStep(NativeBattleBadBreath300Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleBadBreath300Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleBadBreath300Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleBadBreath300Frame, int, NativeBattleBadBreath300AnchorStep> ReadAnchor571400, Func<NativeBattleBadBreath300Frame, NativeBattleBadBreath300PointStep> ReadMouth, Func<NativeBattleBadBreath300Frame, int, byte, short, NativeBattleBadBreath300PointStep> ReadBone502170, Func<NativeBattleBadBreath300Frame, NativeBattleBadBreath300Operation, NativeBattleBadBreath300Frame> Execute, Func<NativeBattleBadBreath300Frame, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleGfAnimationNode, NativeBattleBadBreath300Frame> EmitNode, long Callback)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<NativeBattleBadBreath300Frame, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleGfAnimationNode, NativeBattlePilotVisualIdentity, NativeBattleBadBreath300Frame>? EmitNodePresentation { get; init; }
}
