namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect338Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect338State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect338VibrationStep(NativeBattleEffect338Frame Frame, int Handle);
public sealed record NativeBattleEffect338IoPoll(NativeBattleEffect338Frame Frame, bool Busy);
public sealed record NativeBattleEffect338Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect338Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect338Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect338Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect338Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect338Frame, NativeBattleEffect338Operation, NativeBattleEffect338Frame> Execute, Func<NativeBattleEffect338Frame, NativeBattleEffect338VibrationStep> StartVibration, Func<NativeBattleEffect338Frame, int, NativeBattleEffect338IoPoll> PollIo5342C0, Func<NativeBattleEffect338Frame, uint, int> ReadBoneCount)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect338Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect338Stage5713E0(int Intensity) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338SelectSequence505C00(uint ModelAddress, ushort Sequence) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338Contact506690(int ContactIndex) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338ReadBone502170(uint ModelAddress, int Bone, uint Destination) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338CaptureBounds6DFD90(uint ModelAddress, uint Destination) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338DrawModel6A79B0(uint TaskAddress) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338StopVibration4A2940(int Handle) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338InitializeIo534110(int ArenaOffset) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338QueueFile5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338AdvanceIo534210 : NativeBattleEffect338Operation;
public sealed record NativeBattleEffect338DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect338Operation;
