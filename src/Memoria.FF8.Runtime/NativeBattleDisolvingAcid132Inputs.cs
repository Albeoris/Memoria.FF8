namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDisolvingAcid132Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleDisolvingAcid132State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleDisolvingAcid132PointStep(NativeBattleDisolvingAcid132Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleDisolvingAcid132ActStep(NativeBattleDisolvingAcid132Frame Frame, int Remaining);
public sealed record NativeBattleDisolvingAcid132Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<NativeBattleDisolvingAcid132Frame, NativeBattleEffectArena, NativeBattleDisolvingAcid132PointStep> ReadEmitter6B2B70, Func<NativeBattleDisolvingAcid132Frame, uint, NativeBattlePackedMatrix32> ReadModelMatrix, Func<uint, byte> ReadModelBoneCount, Func<NativeBattleDisolvingAcid132Frame, uint, byte, short, NativeBattleDisolvingAcid132PointStep> ReadBone502170, Func<NativeBattleDisolvingAcid132Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleDisolvingAcid132Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleDisolvingAcid132Frame, uint, NativeBattleGfAnimationSource, int, int, NativeBattleEffectArena, NativeBattleDisolvingAcid132ActStep> AdvanceAct, Func<NativeBattleDisolvingAcid132Frame, NativeBattleDisolvingAcid132Operation, NativeBattleDisolvingAcid132Frame> Execute);

public abstract record NativeBattleDisolvingAcid132Operation;
public sealed record NativeBattleDisolvingAcid132Sound(uint Value, uint Flags, byte Pan) : NativeBattleDisolvingAcid132Operation;
public sealed record NativeBattleDisolvingAcid132Contact(int Index) : NativeBattleDisolvingAcid132Operation;
public sealed record NativeBattleDisolvingAcid132DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleDisolvingAcid132Operation;
public sealed record NativeBattleDisolvingAcid132DrawTemplate180(uint TemporaryAddress, uint SourceAddress) : NativeBattleDisolvingAcid132Operation;
public sealed record NativeBattleDisolvingAcid132DeliverRibbonBatch(NativeBattleGfGeometryBatch Batch) : NativeBattleDisolvingAcid132Operation;
