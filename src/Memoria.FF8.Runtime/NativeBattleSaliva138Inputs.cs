namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSaliva138Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleSaliva138State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleSaliva138PointStep(NativeBattleSaliva138Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleSaliva138ActStep(NativeBattleSaliva138Frame Frame, int Remaining);
public sealed record NativeBattleSaliva138Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<NativeBattleSaliva138Frame, uint, byte, short, NativeBattleSaliva138PointStep> ReadBone502170, Func<NativeBattleSaliva138Frame, NativeBattleSaliva138PointStep> ReadMouth, Func<uint, short> ReadModelYaw, Func<NativeBattleSaliva138Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleSaliva138Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleSaliva138Frame, uint, NativeBattleGfAnimationSource, int, int, NativeBattleEffectArena, NativeBattleSaliva138ActStep> AdvanceAct, Func<NativeBattleSaliva138Frame, NativeBattleSaliva138Operation, NativeBattleSaliva138Frame> Execute);

public abstract record NativeBattleSaliva138Operation;
public sealed record NativeBattleSaliva138Sound(uint Value, uint Flags, byte Pan) : NativeBattleSaliva138Operation;
public sealed record NativeBattleSaliva138Contact(int Index) : NativeBattleSaliva138Operation;
public sealed record NativeBattleSaliva138DrawCustom92(uint TemporaryAddress) : NativeBattleSaliva138Operation;
public sealed record NativeBattleSaliva138DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleSaliva138Operation;
public sealed record NativeBattleSaliva138DeliverRibbonBatch(NativeBattleGfGeometryBatch Batch) : NativeBattleSaliva138Operation;
