namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAdditional181Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleAdditional181State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleAdditional181PointStep(NativeBattleAdditional181Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleAdditional181Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleAdditional181Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleAdditional181Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleAdditional181Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleAdditional181Frame, int, int, int, NativeBattleAdditional181PointStep> ReadBone502170, Func<NativeBattleAdditional181Frame, int, NativeBattleAdditional181PointStep> ReadAnchor571400, Func<NativeBattleAdditional181Frame, NativeBattleAdditional181Operation, NativeBattleAdditional181Frame> Execute);

public abstract record NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181Contact506690(int ContactIndex) : NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181Sound501330(uint Packed, uint Flags, int Pan) : NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181Flash5712C0(int Arg0, int Arg1, int Arg2, int Arg3) : NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181ActorFlags(int Actor, ushort ClearMask, ushort SetMask) : NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181DrawBody128(int Actor, uint TemporaryAddress) : NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAdditional181Operation;
public sealed record NativeBattleAdditional181DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAdditional181Operation;
