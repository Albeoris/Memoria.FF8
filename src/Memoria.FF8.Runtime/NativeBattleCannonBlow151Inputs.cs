namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCannonBlow151Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleCannonBlow151State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleCannonBlow151PointStep(NativeBattleCannonBlow151Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleCannonBlow151Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleCannonBlow151Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleCannonBlow151Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleCannonBlow151Frame, int, NativeBattleActionMatrix> ReadModelWorld, Func<NativeBattleCannonBlow151Frame, int, int, int, NativeBattleCannonBlow151PointStep> ReadBone502170, Func<NativeBattleCannonBlow151Frame, ushort, NativeBattleCannonBlow151PointStep> ReadTargetCenter, Func<NativeBattleCannonBlow151Frame, NativeBattleCannonBlow151Operation, NativeBattleCannonBlow151Frame> Execute);

public abstract record NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151Sound(uint Packed, uint Flags, byte Pan) : NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151ContactsAll(int HeaderIndex) : NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151AdjustShake(short Delta) : NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151DrawScrolling124(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleCannonBlow151Operation;
public sealed record NativeBattleCannonBlow151ReleaseCommon(uint SourceAddress, uint TaskAddress) : NativeBattleCannonBlow151Operation;
