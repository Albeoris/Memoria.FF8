namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBladeShot12Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBladeShot12State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleBladeShot12PointStep(NativeBattleBladeShot12Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleBladeShot12Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleBladeShot12Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleBladeShot12Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleBladeShot12Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleBladeShot12Frame, uint, int, int, NativeBattleBladeShot12PointStep> ReadBone502170, Func<NativeBattleBladeShot12Frame, uint, NativeBattleBladeShot12PointStep> ReadAnchor571400, Func<NativeBattleBladeShot12Frame, NativeBattleBladeShot12Operation, NativeBattleBladeShot12Frame> Execute);

public abstract record NativeBattleBladeShot12Operation;
public sealed record NativeBattleBladeShot12ActorMeshMask124(uint ModelAddress, uint ClearMask, uint SetMask) : NativeBattleBladeShot12Operation;
public sealed record NativeBattleBladeShot12Contact506690(int ContactIndex) : NativeBattleBladeShot12Operation;
public sealed record NativeBattleBladeShot12Sound5013A0(uint Packed, uint Flags, NativeBattleEffectArena Point8) : NativeBattleBladeShot12Operation;
public sealed record NativeBattleBladeShot12DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBladeShot12Operation;
public sealed record NativeBattleBladeShot12DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBladeShot12Operation;
public sealed record NativeBattleBladeShot12DrawCustomMesh88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBladeShot12Operation;
