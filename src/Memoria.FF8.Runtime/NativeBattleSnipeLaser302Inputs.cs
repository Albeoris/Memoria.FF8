namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSnipeLaser302Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleSnipeLaser302State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleSnipeLaser302PointStep(NativeBattleSnipeLaser302Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleSnipeLaser302Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleSnipeLaser302Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleSnipeLaser302Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleSnipeLaser302Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleSnipeLaser302Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleSnipeLaser302Frame, uint, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleSnipeLaser302Frame, uint, int, int, NativeBattleSnipeLaser302PointStep> ReadBone502170, Func<NativeBattleSnipeLaser302Frame, uint, NativeBattleSnipeLaser302PointStep> ReadAnchor571400, Func<NativeBattleSnipeLaser302Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleSnipeLaser302Frame, NativeBattleSnipeLaser302Operation, NativeBattleSnipeLaser302Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleSnipeLaser302Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleSnipeLaser302Contact506690(int ContactIndex) : NativeBattleSnipeLaser302Operation;
public sealed record NativeBattleSnipeLaser302Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleSnipeLaser302Operation;
public sealed record NativeBattleSnipeLaser302DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleSnipeLaser302Operation;
public sealed record NativeBattleSnipeLaser302DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleSnipeLaser302Operation;
public sealed record NativeBattleSnipeLaser302DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleSnipeLaser302Operation;
