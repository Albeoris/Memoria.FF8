namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect183Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffect183State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect183PointStep(NativeBattleEffect183Frame Frame, NativeBattleEffectArena Point8);
public abstract record NativeBattleEffect183Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect183Sound(uint Packed, NativeBattleEffectArena Anchor8) : NativeBattleEffect183Operation;
public sealed record NativeBattleEffect183ContactFirst(int HeaderIndex) : NativeBattleEffect183Operation;
public sealed record NativeBattleEffect183DrawWindowed144(uint TemporaryAddress, uint SourceAddress) : NativeBattleEffect183Operation;
public sealed record NativeBattleEffect183Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleEffect183Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect183Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect183Frame, int, int, int, NativeBattleEffect183PointStep> ReadBone502170, Func<int, short> ReadHeading, Func<NativeBattleEffect183Frame, int, NativeBattleEffect183PointStep> ReadAnchor571400, Func<NativeBattleEffect183Frame, NativeBattleEffect183Operation, NativeBattleEffect183Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}
