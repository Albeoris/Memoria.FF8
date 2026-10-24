namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect76Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect76State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect76Step(NativeBattleEffect76Frame Frame, int Result);
public sealed record NativeBattleEffect76FactoryStep(NativeBattleEffect76Frame Frame, uint Address);
public sealed record NativeBattleEffect76PointStep(NativeBattleEffect76Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect76Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect76Frame, uint> ReadFlags, Func<NativeBattleEffect76Frame, NativeBattleEffect76Step> ReadStep508500, Func<NativeBattleEffect76Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect76Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect76Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect76Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect76Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect76Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect76Frame, int, NativeBattleEffectArena> ReadPosition8, Func<NativeBattleEffect76Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect76PointStep> Point502170, Func<NativeBattleEffect76Frame, NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleEffect76Frame, short?> ReadEyePadding, Func<NativeBattleEffect76Frame, short?> ReadTargetPadding, Func<NativeBattleEffect76Frame, int, bool> ReadVibrationPlaying4A2900, Func<NativeBattleEffect76Frame, uint, uint> ReadColor, Func<NativeBattleEffect76Frame, NativeBattleEffect76Operation, NativeBattleEffect76Frame> Execute);

public abstract record NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76UploadTim505E30(uint SourceAddress) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76SetMode4A8480(int Mode) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76ModelFlags(int Actor, ushort AndMask, ushort OrMask, bool WordWidth) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76ModelPointY(int Actor, short Value, bool Add) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Stage5713E0(int Intensity, int Color) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76MusicLoad501A20(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76MusicVolume501B10(uint Duration, byte Gain) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76QueueStage512AA0(int Index, uint DestinationAddress) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76ReloadStage512AC0(int Index, uint SourceAddress) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76SyncModelColors61FC60(uint Color) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76WaveRequest46CBB0(int Channel, string ResourceName, int Gain) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Sequence505C00(int Actor, byte Sequence) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Text4876F0(int Actor, ushort Index, ushort Duration) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Contact506690(int Index) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76StopVibration4A2940(int Handle) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76ForceCamera50A730 : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76CaptureCameraRetirement : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76PublishCameraPose(NativeBattleCameraPose Pose, short? EyePadding, short? TargetPadding) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76GraphicsFlags(uint AndMask, uint OrMask) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76PcRender47CF50(int Mode, byte Red, byte Green, byte Blue, byte Depth) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76Flash5712C0(int Rise, int Hold, int Fall, byte Color) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect76Operation;
public sealed record NativeBattleEffect76CommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect76Operation;
