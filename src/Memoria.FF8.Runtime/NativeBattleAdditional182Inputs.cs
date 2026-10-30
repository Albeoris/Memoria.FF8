namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAdditional182Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleAdditional182State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleAdditional182PointStep(NativeBattleAdditional182Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleAdditional182Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleAdditional182Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleAdditional182Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleAdditional182Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleAdditional182Frame, int, NativeBattleAdditional182PointStep> ReadAnchor571400, Func<NativeBattleAdditional182Frame, NativeBattleAdditional182Operation, NativeBattleAdditional182Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleAdditional182Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleAdditional182Sound5013A0(uint Packed, uint Flags, NativeBattleEffectArena Point8) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182ContactsAll(int HeaderIndex) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182ActorFlags(int Actor, ushort ClearMask, ushort SetMask) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182ActorAlpha(int Actor, byte Alpha) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182ActorRecolor(int Actor, int Cue) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182Stage5713E0(int Value, uint Flags) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAdditional182Operation;
public sealed record NativeBattleAdditional182DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleAdditional182Operation;
