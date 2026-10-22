namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHypnotize133Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleHypnotize133State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public enum NativeBattleHypnotize133ContextKind
{
    Main,
    Anchored,
    Fading
}

public sealed record NativeBattleHypnotize133PointStep(NativeBattleHypnotize133Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleHypnotize133ContextStep(NativeBattleHypnotize133Frame Frame, NativeBattleEffectArena Context40);
public sealed record NativeBattleHypnotize133ActStep(NativeBattleHypnotize133Frame Frame, int Remaining);
public abstract record NativeBattleHypnotize133Operation;
public sealed record NativeBattleHypnotize133Sound(uint Packed) : NativeBattleHypnotize133Operation;
public sealed record NativeBattleHypnotize133BulkContact(int First, int Count) : NativeBattleHypnotize133Operation;
public sealed record NativeBattleHypnotize133DrawSprite180(uint TaskAddress, uint Temporary180) : NativeBattleHypnotize133Operation;
public sealed record NativeBattleHypnotize133Inputs(NativeBattleActionPacketsState Packets, Func<int, byte> ReadBoneCount, Func<NativeBattleHypnotize133Frame, int, byte, short, NativeBattleHypnotize133PointStep> ReadBone502170, Func<NativeBattleHypnotize133Frame, uint, NativeBattleHypnotize133ContextKind, NativeBattleHypnotize133ContextStep> PrepareActContext, Func<NativeBattleHypnotize133Frame, uint, int, int, NativeBattleGfAnimationSource, NativeBattleEffectArena, NativeBattleHypnotize133ActStep> AdvanceAct, Func<NativeBattleHypnotize133Frame, NativeBattleHypnotize133Operation, NativeBattleHypnotize133Frame> Execute);
