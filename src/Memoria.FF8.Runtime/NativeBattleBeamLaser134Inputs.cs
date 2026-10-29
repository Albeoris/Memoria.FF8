namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBeamLaser134Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBeamLaser134State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleBeamLaser134PointStep(NativeBattleBeamLaser134Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleBeamLaser134AnchorStep(NativeBattleBeamLaser134Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleBeamLaser134ContextStep(NativeBattleBeamLaser134Frame Frame, NativeBattleEffectArena Context40);
public sealed record NativeBattleBeamLaser134RotateStep(NativeBattleBeamLaser134Frame Frame, NativeBattleEffectArena Context40, NativeBattleVector UpdatedLocal);
public sealed record NativeBattleBeamLaser134ActStep(NativeBattleBeamLaser134Frame Frame, int Remaining);
public abstract record NativeBattleBeamLaser134Operation;
public sealed record NativeBattleBeamLaser134ActorFlags(int Actor, ushort Mask, ushort Bits) : NativeBattleBeamLaser134Operation;
public sealed record NativeBattleBeamLaser134Sound(uint Packed) : NativeBattleBeamLaser134Operation;
public sealed record NativeBattleBeamLaser134Contact(int Index) : NativeBattleBeamLaser134Operation;
public sealed record NativeBattleBeamLaser134DrawSprite180(uint TaskAddress, uint Temporary180, bool Moving) : NativeBattleBeamLaser134Operation;
public sealed record NativeBattleBeamLaser134DrawStatic88(NativeBattlePackedMatrix32 World, int FadeCue) : NativeBattleBeamLaser134Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleBeamLaser134Inputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleBeamLaser134Frame, int, byte, short, NativeBattleBeamLaser134PointStep> ReadBone502170, Func<NativeBattleBeamLaser134Frame, int, NativeBattleBeamLaser134AnchorStep> ReadAnchor571400, Func<NativeBattleBeamLaser134Frame, NativeBattlePackedMatrix32, NativeBattleVector, short?, NativeBattleVector, NativeBattleBeamLaser134RotateStep> RotatePoint, Func<NativeBattleBeamLaser134Frame, uint, NativeBattleBeamLaser134ContextStep> PrepareChildContext, Func<NativeBattleBeamLaser134Frame, NativeBattlePackedMatrix32, int, NativeBattleBeamLaser134ContextStep> PrepareWorldContext, Func<NativeBattleBeamLaser134Frame, uint, int, int, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleBeamLaser134ActStep> AdvanceAct, Func<NativeBattleBeamLaser134Frame, NativeBattleBeamLaser134Operation, NativeBattleBeamLaser134Frame> Execute);
