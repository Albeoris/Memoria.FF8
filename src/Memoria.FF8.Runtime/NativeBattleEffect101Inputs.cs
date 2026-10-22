namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect101Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect101State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleEffect101PositionStep(NativeBattleEffect101Frame Frame, NativeBattleEffectArena Position8);
public sealed record NativeBattleEffect101WordStep(NativeBattleEffect101Frame Frame, short Value);
public sealed record NativeBattleEffect101PointStep(NativeBattleEffect101Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleEffect101ActStep(NativeBattleEffect101Frame Frame, int Remaining);
public abstract record NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101ActorFlags(int Actor, ushort Mask, ushort Bits) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101PositionXYZ(int Actor, NativeBattleVector Position) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101PositionY(int Actor, short Y) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101Position8(int Actor, NativeBattleEffectArena Data) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101Color44(int Actor, uint Value) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101ScalePointer(int Actor, uint Address) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101WorldR9TXYZ(int Actor, NativeBattleActionMatrix Matrix) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101Stage(short Depth, uint Color) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101RingSequence(int Actor, byte Sequence) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101Sound(uint Packed) : NativeBattleEffect101Operation;
public sealed record NativeBattleEffect101Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect101Frame, int, NativeBattleEffect101PositionStep> ReadPosition8, Func<NativeBattleEffect101Frame, int, NativeBattleEffect101WordStep> ReadBodyBone0X, Func<NativeBattleEffect101Frame, uint, int, int, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleEffect101ActStep> AdvanceAct, Func<NativeBattleEffect101Frame, int, NativeBattleEffectArena, short, short, NativeBattleEffect101PointStep> BlendPosition56CB90, Func<NativeBattleEffect101Frame, NativeBattleEffect101Operation, NativeBattleEffect101Frame> Execute, NativeBattleSummonColors Colors);
