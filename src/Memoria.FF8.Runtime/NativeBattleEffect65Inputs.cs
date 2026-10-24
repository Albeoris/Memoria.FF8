namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect65Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect65State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect65Step(NativeBattleEffect65Frame Frame, int Result);
public sealed record NativeBattleEffect65PointStep(NativeBattleEffect65Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect65PointOutputStep(NativeBattleEffect65Frame Frame, NativeBattleEffectArena Output);
public sealed record NativeBattleEffect65IoStep(NativeBattleEffect65Frame Frame, bool Busy);
public sealed record NativeBattleEffect65IoCompletionStep(NativeBattleEffect65Frame Frame, bool Complete);
public sealed record NativeBattleEffect65Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect65Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect65Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect65Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect65Frame, short> ReadCameraRoll, Func<NativeBattleEffect65Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect65Frame, uint, NativeBattleEffectArena> ReadPosition8, Func<NativeBattleEffect65Frame, uint, NativeBattleEffect65PointStep> ReadAnchor571400, Func<NativeBattleEffect65Frame, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect65PointOutputStep> ReadCasterPoint6E2CB0, Func<NativeBattleEffect65Frame, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect65PointOutputStep> ReadCasterPoint6E2D30, Func<NativeBattleEffect65Frame, NativeBattleEffect65IoStep> Poll534270, Func<NativeBattleEffect65Frame, int, NativeBattleEffect65IoCompletionStep> Select534300, Func<NativeBattleEffect65Frame, NativeBattleEffect65Operation, NativeBattleEffect65Frame> Execute);

public abstract record NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65UploadTim505E30(uint SourceAddress) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65StopVibration4A2940(int Handle) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65ModelFlags(uint ModelAddress, ushort AndMask, ushort OrMask) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65WriteActorPoint8(uint ModelAddress, NativeBattleEffectArena Point8, bool PublishWorldXYZ) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65WriteActorPositionXYZ(uint ModelAddress, NativeBattleVector Position, bool PublishWorldXYZ) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65SetActorScalePointer30(uint ModelAddress, uint Address) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65SetSequence505C00(uint ModelAddress, byte Sequence) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65StageFlags(byte AndMask, byte OrMask, int? Group = null) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65CameraForce50A730 : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65CameraPoseXYZ(NativeBattleCameraPoint Eye, NativeBattleCameraPoint Target) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65CameraRoll(short Roll) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65CameraRestoreCaptured503300 : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65PcRenderRequest47CF50(int Mode, int Red, int Green, int Blue, int Alpha) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65Contacts506BA0(int HeaderIndex) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65IoInitialize534110(uint ControlAddress) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65IoQueue5341D0(short FileId, uint DestinationAddress, byte Type) : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65IoAdvance534210 : NativeBattleEffect65Operation;
public sealed record NativeBattleEffect65DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect65Operation;
