namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMiniPulseCannon286Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMiniPulseCannon286State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public sealed record NativeBattleMiniPulseCannon286PointStep(NativeBattleMiniPulseCannon286Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleMiniPulseCannon286MuzzleStep(NativeBattleMiniPulseCannon286Frame Frame, int X, int Y, int Z);
public sealed record NativeBattleMiniPulseCannon286Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleMiniPulseCannon286Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleMiniPulseCannon286Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleMiniPulseCannon286Frame, BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleMiniPulseCannon286Frame, BattleModelOwner, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleMiniPulseCannon286Frame, BattleModelOwner, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleMiniPulseCannon286Frame, BattleModelOwner, NativeBattleMiniPulseCannon286PointStep> ReadAnchor571400, Func<NativeBattleMiniPulseCannon286Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleMiniPulseCannon286Frame, NativeBattleMiniPulseCannon286Operation, NativeBattleMiniPulseCannon286Frame> Execute);

public abstract record NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286ComposeCaster5095B0(BattleModelOwner Model) : NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286RestoreCaster508C90(BattleModelOwner Model) : NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286ActorFlags(BattleModelOwner Model, ushort ClearMask, ushort SetMask) : NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286Contact506690(BattleContactOccurrence Occurrence) : NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286DrawWindow108(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMiniPulseCannon286Operation;
public sealed record NativeBattleMiniPulseCannon286DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMiniPulseCannon286Operation;
