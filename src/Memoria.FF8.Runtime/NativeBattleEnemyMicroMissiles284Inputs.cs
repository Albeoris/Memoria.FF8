namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnemyMicroMissiles284Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEnemyMicroMissiles284State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEnemyMicroMissiles284PointStep(NativeBattleEnemyMicroMissiles284Frame Frame, NativeBattleEffectArena Point8, int Result);
public sealed record NativeBattleEnemyMicroMissiles284Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleEnemyMicroMissiles284Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEnemyMicroMissiles284Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEnemyMicroMissiles284Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEnemyMicroMissiles284Frame, uint, int> ReadBoneCount, Func<NativeBattleEnemyMicroMissiles284Frame, uint, int, short> ReadBoneParent, Func<NativeBattleEnemyMicroMissiles284Frame, uint, int, byte> ReadBoneAlias, Func<NativeBattleEnemyMicroMissiles284Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleEnemyMicroMissiles284Frame, uint, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleEnemyMicroMissiles284Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEnemyMicroMissiles284Frame, NativeBattleEnemyMicroMissiles284Operation, NativeBattleEnemyMicroMissiles284Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEnemyMicroMissiles284Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEnemyMicroMissiles284Contact506690(int ContactIndex) : NativeBattleEnemyMicroMissiles284Operation;
public sealed record NativeBattleEnemyMicroMissiles284Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEnemyMicroMissiles284Operation;
public sealed record NativeBattleEnemyMicroMissiles284DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEnemyMicroMissiles284Operation;
public sealed record NativeBattleEnemyMicroMissiles284DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEnemyMicroMissiles284Operation;
public sealed record NativeBattleEnemyMicroMissiles284Reaction505C00(uint ModelAddress, byte Sequence) : NativeBattleEnemyMicroMissiles284Operation;
