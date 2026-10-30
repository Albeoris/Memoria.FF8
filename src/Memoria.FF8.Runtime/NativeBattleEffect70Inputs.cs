namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect70Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect70State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect70Step(NativeBattleEffect70Frame Frame, int Result);
public sealed record NativeBattleEffect70PointStep(NativeBattleEffect70Frame Frame, NativeBattleEffectArena Point8);

public sealed partial record NativeBattleEffect70Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect70Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect70Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect70Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect70Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect70Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect70Frame, short> ReadCameraRoll, Func<NativeBattleEffect70Frame, uint, uint> ReadColor, Func<NativeBattleEffect70Frame, NativeBattleEffect70Operation, NativeBattleEffect70Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
    public Func<NativeBattleEffect70Frame, int, NativeBattleEffectArena>? ReadPosition8 { get; init; }
    public Func<NativeBattleEffect70Frame, int, NativeBattleEffect70PointStep>? ReadAnchor571400 { get; init; }
    public Func<NativeBattleEffect70Frame, int, NativeBattleEffect70Step>? SelectIo5342C0 { get; init; }
    public Func<NativeBattleEffect70Frame, ushort>? ReadSavedFocal { get; init; }
}

public abstract record NativeBattleEffect70Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect70UploadTim(uint Source) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70ModelFlags(int Actor, ushort AndMask, ushort OrMask, bool WordWidth) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70ModelPositionWord(int Actor, int Offset, short Value) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70ModelPositionPacked(int Actor, NativeBattleEffectArena Position8) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70CaptureBounds6DFD90(int Actor, uint Destination) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70DrawActor693170(int Actor, NativeBattleEffectArena Rotation8, NativeBattleEffectArena Offset8, short Scale) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70StageIntensity5713E0(int Depth, int Flags) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70StageFlags(int Group, byte AndMask, byte OrMask) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70CameraInitialize50A730 : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70CameraPose(NativeBattleCameraPose Pose) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70CameraFocal(ushort Value) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70CameraRoll(short Value) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70Sound501330(uint Descriptor, uint Flags, byte Pan) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70StartVibration(NativeVibrationBank Bank, uint TaskAddress, int OutputOffset) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70StopVibration(int Handle) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70IoInitialize534110(uint ControlAddress) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70IoQueue5341D0(short FileId, uint Destination, byte Type) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70IoAdvance534210 : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70ConsumeContacts506BA0 : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70DeliverPrivate(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70DeliverCommon(NativeBattleGfGeometryBatch Batch) : NativeBattleEffect70Operation;
public sealed record NativeBattleEffect70QueryBonePoint(int Actor, short Selector, uint Destination) : NativeBattleEffect70Operation;
