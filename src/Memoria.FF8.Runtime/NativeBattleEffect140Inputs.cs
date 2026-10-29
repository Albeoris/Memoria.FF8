namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect140Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect140State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect140Step(NativeBattleEffect140Frame Frame, int Result);
public sealed record NativeBattleEffect140FactoryStep(NativeBattleEffect140Frame Frame, uint Address);
public sealed record NativeBattleEffect140IoStep(NativeBattleEffect140Frame Frame, bool Busy);
public sealed record NativeBattleEffect140Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect140Frame, uint> ReadFlags, Func<NativeBattleEffect140Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect140Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect140Frame, int, NativeBattleEffectArena> ReadModelPosition8, Func<NativeBattleEffect140Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect140Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect140Frame, NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleEffect140Frame, short?> ReadEyePadding, Func<NativeBattleEffect140Frame, short?> ReadTargetPadding, Func<NativeBattleEffect140Frame, NativeBattleEffect140Operation, NativeBattleEffect140Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    public Func<NativeBattleEffect140Frame, uint>? ReadCommonSize { get; init; }
    public Func<NativeBattleEffect140Frame, NativeBattleEffect140IoStep>? Poll534270 { get; init; }
}

public abstract record NativeBattleEffect140Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect140UploadTim505E30(uint SourceAddress) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140InitializeIo534110(uint ControllerAddress) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140QueueIo5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140AdvanceIo534210 : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140Stream5018C0(uint Pan, uint Volume) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140StopVibration4A2940(int Handle) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140Stage5713E0(int Intensity, int Color) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140ContactAll506BA0 : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140CommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140ModelColor40(int Actor, uint Color) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140ModelFlags(int Actor, ushort AndMask, ushort OrMask) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140PublishCamera(NativeBattleCameraPose Pose, short? EyePadding, short? TargetPadding) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140DrawOwnedModel(uint ModelAddress, uint ScratchAddress, int FarInput, bool Custom) : NativeBattleEffect140Operation;
public sealed record NativeBattleEffect140DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect140Operation;
