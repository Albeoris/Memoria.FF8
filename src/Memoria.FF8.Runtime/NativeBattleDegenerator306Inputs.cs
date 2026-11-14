namespace Memoria.FF8.Runtime;

public abstract record NativeBattleDegenerator306Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleDegenerator306BindCaster(BattleModelOwner Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306RingSequence(byte Actor, byte Sequence) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306Sound(uint Value, int Pan) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306Contact(int Index) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306Stage(short Depth, uint ColorFlags) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306StageFlags(byte ClearMask, byte SetMask) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306ActorFlags(BattleModelOwner Actor, ushort ClearMask, ushort SetMask) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306ActorRotation8(BattleModelOwner Actor, NativeBattleEffectArena Packed8) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306ActorPosition(BattleModelOwner Actor, NativeBattleVector Position) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306ActorPackedPosition8(BattleModelOwner Actor, NativeBattleEffectArena Packed8) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306ActorAlpha(BattleModelOwner Actor, byte Value) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306ActorRecolor(BattleModelOwner Actor, int Cue) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306RestoreShadow(BattleModelOwner Actor) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306CommonTexture(uint SourceAddress, uint CompletionAddress) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket, int DepthShift) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306DrawCustom108(uint TemporaryAddress) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306DrawBody136(uint TemporaryAddress, byte Actor) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, byte DepthShift = 2) : NativeBattleDegenerator306Operation;
public sealed record NativeBattleDegenerator306AnchorStep(NativeBattleDegenerator306Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleDegenerator306PointStep(NativeBattleDegenerator306Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleDegenerator306Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleDegenerator306Frame, NativeBattlePackedMatrix32> ReadView, Func<short> ReadFocalShadow, Func<int, NativeBattleEffectArena> ReadRotation8, Func<NativeBattleDegenerator306Frame, BattleModelOwner, short, short, NativeBattleDegenerator306PointStep> ReadBone502170, Func<NativeBattleDegenerator306Frame, BattleModelOwner, NativeBattleDegenerator306AnchorStep> ReadAnchor571400, Func<NativeBattleDegenerator306Frame, NativeBattleDegenerator306Operation, NativeBattleDegenerator306Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}
