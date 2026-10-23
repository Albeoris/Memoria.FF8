namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSonic139Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleSonic139State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleSonic139PointStep(NativeBattleSonic139Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleSonic139ActContext(NativeBattlePackedMatrix32 Matrix, int DepthBias, uint MorphAddress);
public sealed record NativeBattleSonic139ActStep(NativeBattleSonic139Frame Frame, int Remaining);
public sealed record NativeBattleSonic139Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleSonic139Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleSonic139Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleSonic139Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleSonic139Frame, uint, short> ReadHeading, Func<NativeBattleSonic139Frame, NativeBattleEffectArena, NativeBattleSonic139PointStep> ReadEmitter6A9200, Func<NativeBattleSonic139Frame, uint, NativeBattleSonic139PointStep> ReadAnchor571400, Func<NativeBattleSonic139Frame, uint, NativeBattleGfAnimationSource, NativeBattleSonic139ActContext, NativeBattleSonic139ActStep> AdvanceAct, Func<NativeBattleSonic139Frame, NativeBattleSonic139Operation, NativeBattleSonic139Frame> Execute);

public abstract record NativeBattleSonic139Operation;
public sealed record NativeBattleSonic139Sound(uint Packed, uint Flags, byte Pan) : NativeBattleSonic139Operation;
public sealed record NativeBattleSonic139WriteActorColor(uint ActorAddress, uint Color) : NativeBattleSonic139Operation;
public sealed record NativeBattleSonic139SetActorFlags(uint ActorAddress, ushort Set, ushort Clear) : NativeBattleSonic139Operation;
public sealed record NativeBattleSonic139ContactForActor(uint ActorAddress) : NativeBattleSonic139Operation;
public sealed record NativeBattleSonic139RequestCommonCompletion(uint SourceAddress, uint FlagAddress) : NativeBattleSonic139Operation;
public sealed record NativeBattleSonic139AppendPacket(uint Address, int Allocation, int Written, int Bucket, ushort Flags, byte[] Data, bool[] Known) : NativeBattleSonic139Operation
{
    public NativeBattleGfPacketByteState[] WriteMask { get; init; } = [];
}
