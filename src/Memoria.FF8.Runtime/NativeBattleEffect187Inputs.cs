namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect187Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect187State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect187Step(NativeBattleEffect187Frame Frame, int Result);
public sealed record NativeBattleEffect187PointStep(NativeBattleEffect187Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect187Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect187Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect187Frame, uint> ReadFlags, Func<NativeBattleEffect187Frame, NativeBattleEffect187Step> ReadStep508500, Func<NativeBattleEffect187Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect187Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect187Frame, short> ReadCameraRoll, Func<NativeBattleEffect187Frame, NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleEffect187Frame, short?> ReadEyePadding, Func<NativeBattleEffect187Frame, short?> ReadTargetPadding, Func<NativeBattleEffect187Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect187Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect187Frame, ushort, NativeBattleEffectArena, NativeBattleEffectArena> ReadTargetCenter5020A0, Func<NativeBattleEffect187Frame, int, int, int, NativeBattleEffectArena, NativeBattleEffect187PointStep> ReadBone502170, Func<NativeBattleEffect187Frame, int, bool> ReadVibrationPlaying4A2900, Func<NativeBattleEffect187Frame, NativeBattleEffect187Operation, NativeBattleEffect187Frame> Execute)
{
    public NativeBattlePrimitiveCapabilities? PrimitiveCapabilities { get; init; }
}

public abstract record NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187UploadTim505E30(uint SourceAddress) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187UploadPixels505DF0(uint RectangleAddress, uint SourceAddress) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187Queue508480(short FileId, uint DestinationAddress) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187PublishStream501860(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187PlayStream5018C0(uint Pan, uint DebugOperand, uint Volume) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187StopVibration4A2940(int Handle) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187Stage5713E0(int Intensity, int Selector) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187StageGroupFlags(byte AndMask, byte OrMask) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187ModelFlags(int Actor, ushort AndMask, ushort OrMask) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187ModelColor(int Actor, uint Value) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187PublishCameraPose(NativeBattleCameraPose Pose, short? EyePadding, short? TargetPadding) : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187Contacts506BA0 : NativeBattleEffect187Operation;
public sealed record NativeBattleEffect187DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect187Operation;

public sealed record NativeBattleEffect187Flash5712C0(int Rise, int Hold, int Fall, int Intensity) : NativeBattleEffect187Operation;

public sealed record NativeBattleEffect187QueueTextureRelease508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect187Operation;
