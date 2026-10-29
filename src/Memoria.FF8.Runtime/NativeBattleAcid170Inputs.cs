namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAcid170Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleAcid170State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleAcid170PointStep(NativeBattleAcid170Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleAcid170Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleAcid170Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleAcid170Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleAcid170Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleAcid170Frame, int, int, int, NativeBattleAcid170PointStep> ReadBone502170, Func<NativeBattleAcid170Frame, int, NativeBattleAcid170PointStep> ReadAnchor571400, Func<NativeBattleAcid170Frame, NativeBattleAcid170Operation, NativeBattleAcid170Frame> Execute);

public abstract record NativeBattleAcid170Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleAcid170Contact506690(int ContactIndex) : NativeBattleAcid170Operation;
public sealed record NativeBattleAcid170Sound501330(uint Packed, uint Flags, int Pan) : NativeBattleAcid170Operation;
public sealed record NativeBattleAcid170ActorFlags(int Actor, ushort ClearMask, ushort SetMask) : NativeBattleAcid170Operation;
public sealed record NativeBattleAcid170DrawBody172(int Actor, uint TemporaryAddress) : NativeBattleAcid170Operation;
public sealed record NativeBattleAcid170DrawMesh92(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAcid170Operation;
public sealed record NativeBattleAcid170DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAcid170Operation;
public sealed record NativeBattleAcid170DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAcid170Operation;
