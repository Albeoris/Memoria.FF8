namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect326Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect326State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect326Step(NativeBattleEffect326Frame Frame, int Result);
public sealed record NativeBattleEffect326PointStep(NativeBattleEffect326Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect326Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect326Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect326Frame, uint> ReadFlags, Func<NativeBattleEffect326Frame, NativeBattleEffect326Step> ReadStep508500, Func<NativeBattleEffect326Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect326Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect326Frame, short> ReadCameraRoll, Func<NativeBattleEffect326Frame, NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleEffect326Frame, short?> ReadEyePadding, Func<NativeBattleEffect326Frame, short?> ReadTargetPadding, Func<NativeBattleEffect326Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect326Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect326Frame, ushort, NativeBattleEffectArena, NativeBattleEffectArena> ReadTargetCenter5020A0, Func<NativeBattleEffect326Frame, int, int, int, NativeBattleEffectArena, NativeBattleEffect326PointStep> ReadBone502170, Func<NativeBattleEffect326Frame, int, bool> ReadVibrationPlaying4A2900, Func<NativeBattleEffect326Frame, NativeBattleEffect326Operation, NativeBattleEffect326Frame> Execute)
{
    public byte? EntryLifecycle { get; init; }
    public Func<NativeBattleEffect326Frame, int, NativeBattleEffectArena, NativeBattleEffect326PointStep>? ReadAnchor571400 { get; init; }
    public NativeBattleShadowSource? ShadowSource { get; init; }
    public NativeBattlePrimitiveCapabilities? PrimitiveCapabilities { get; init; }
    public Func<NativeBattleEffect326Frame, uint, uint>? ReadColor { get; init; }
}

public abstract record NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326UploadTim505E30(uint SourceAddress) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326UploadPixels505DF0(uint RectangleAddress, uint SourceAddress) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326Queue508480(short FileId, uint DestinationAddress) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326PublishStream501860(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326PlayStream5018C0(uint Pan, uint DebugOperand, uint Volume) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326StopVibration4A2940(int Handle) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326Stage5713E0(int Intensity, int Selector) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326StageGroupFlags(byte AndMask, byte OrMask) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326ModelFlags(int Actor, ushort AndMask, ushort OrMask) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326ModelColor(int Actor, uint Value) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326PublishCameraPose(NativeBattleCameraPose Pose, short? EyePadding, short? TargetPadding) : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326Contacts506BA0 : NativeBattleEffect326Operation;
public sealed record NativeBattleEffect326DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect326Operation;

public sealed record NativeBattleEffect326Flash5712C0(int Rise, int Hold, int Fall, int Intensity) : NativeBattleEffect326Operation;

public sealed record NativeBattleEffect326QueueTextureRelease508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect326Operation;

public sealed record NativeBattleEffect326WriteModelBytes(int Actor, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect326Operation;

public sealed record NativeBattleEffect326ActorAnimation505C00(int Actor, short Animation) : NativeBattleEffect326Operation;
