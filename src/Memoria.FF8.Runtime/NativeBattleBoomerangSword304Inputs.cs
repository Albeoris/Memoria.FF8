namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBoomerangSword304Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBoomerangSword304State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleBoomerangSword304PointStep(NativeBattleBoomerangSword304Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleBoomerangSword304Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleBoomerangSword304Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleBoomerangSword304Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleBoomerangSword304Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleBoomerangSword304Frame, uint, int, int, NativeBattleBoomerangSword304PointStep> ReadBone502170, Func<NativeBattleBoomerangSword304Frame, uint, NativeBattleBoomerangSword304PointStep> ReadAnchor571400, Func<NativeBattleBoomerangSword304Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleBoomerangSword304Frame, NativeBattleBoomerangSword304Operation, NativeBattleBoomerangSword304Frame> Execute);

public abstract record NativeBattleBoomerangSword304Operation;
public sealed record NativeBattleBoomerangSword304Contact506690(int ContactIndex) : NativeBattleBoomerangSword304Operation;
public sealed record NativeBattleBoomerangSword304Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleBoomerangSword304Operation;
public sealed record NativeBattleBoomerangSword304DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBoomerangSword304Operation;
public sealed record NativeBattleBoomerangSword304DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBoomerangSword304Operation;
public sealed record NativeBattleBoomerangSword304DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBoomerangSword304Operation;

public sealed record NativeBattleBoomerangSword304ActorMeshMask124(uint ModelAddress, uint ClearMask, uint SetMask) : NativeBattleBoomerangSword304Operation;
