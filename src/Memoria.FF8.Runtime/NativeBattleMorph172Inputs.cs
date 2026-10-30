namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMorph172Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMorph172State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleMorph172PointStep(NativeBattleMorph172Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleMorph172Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleMorph172Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleMorph172Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleMorph172Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleMorph172Frame, int, NativeBattleMorph172PointStep> ReadAnchor571400, Func<NativeBattleMorph172Frame, int, int> ReadBoneCount, Func<NativeBattleMorph172Frame, int, int, int, NativeBattleMorph172PointStep> ReadBone502170, Func<NativeBattleMorph172Frame, NativeBattleMorph172Operation, NativeBattleMorph172Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleMorph172Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleMorph172Sound5013A0(uint Packed, uint Flags, NativeBattleEffectArena Point8) : NativeBattleMorph172Operation;
public sealed record NativeBattleMorph172ActorFlags(int Actor, ushort ClearMask, ushort SetMask) : NativeBattleMorph172Operation;
public sealed record NativeBattleMorph172DrawBody220(int Actor, uint TemporaryAddress) : NativeBattleMorph172Operation;
public sealed record NativeBattleMorph172DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMorph172Operation;
public sealed record NativeBattleMorph172DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMorph172Operation;
