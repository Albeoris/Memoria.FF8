namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect69Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect69State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect69Step(NativeBattleEffect69Frame Frame, int Result);

public sealed partial record NativeBattleEffect69Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect69Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect69Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect69Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect69Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect69Frame, uint> ReadFlags, Func<NativeBattleEffect69Frame, short> ReadCameraRoll, Func<NativeBattleEffect69Frame, uint, uint> ReadColor, Func<NativeBattleEffect69Frame, NativeBattleEffect69Operation, NativeBattleEffect69Frame> Execute)
{
    public bool CapturePresentation { get; init; }
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }

    public Func<NativeBattleEffect69Frame, int>? ReadIoResult { get; init; }
    public Func<NativeBattleEffect69Frame, uint>? ReadScratchAddress { get; init; }
}

public abstract record NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69ModelFlags(int Actor, ushort AndMask, ushort OrMask, bool WordWidth) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69ModelPositionZ(int Actor, short Z, bool WriteWorldTranslation = false) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69StageFlags(int Group, byte AndMask, byte OrMask) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69Sound501330(uint Descriptor, uint Flags, byte Pan) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69StartVibration(NativeVibrationBank Bank, uint TaskAddress, int OutputOffset) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69StopVibration(int Handle) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69QueueStage512AA0(int Index, uint Destination) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69ReloadStage512AC0(int Index, uint Source) : NativeBattleEffect69Operation;
public sealed record NativeBattleEffect69DrawCaster(uint ModelAddress, int Threshold, uint ScratchAddress) : NativeBattleEffect69Operation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect69DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect69Operation;

public sealed record NativeBattleEffect69ModelColors44(uint Color) : NativeBattleEffect69Operation;

public sealed record NativeBattleEffect69DrawWindow108(uint TemporaryAddress, uint GeometryAddress, uint SelectedVertices) : NativeBattleEffect69Operation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
