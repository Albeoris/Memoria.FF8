namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect66Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect66State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect66Step(NativeBattleEffect66Frame Frame, int Result);
public sealed record NativeBattleEffect66PointStep(NativeBattleEffect66Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect66Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect66Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect66Frame, uint> ReadFlags, Func<NativeBattleEffect66Frame, NativeBattleEffect66Step> ReadStep508500, Func<NativeBattleEffect66Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect66Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect66Frame, short> ReadCameraRoll, Func<NativeBattleEffect66Frame, NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleEffect66Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect66Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect66Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect66Frame, int, int, int, NativeBattleEffectArena, NativeBattleEffect66PointStep> ReadBone502170, Func<NativeBattleEffect66Frame, int, bool> ReadVibrationPlaying4A2900, Func<NativeBattleEffect66Frame, NativeBattleEffect66Operation, NativeBattleEffect66Frame> Execute);

public abstract record NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66BeginCamera63E960(uint SourceAddress, uint MatrixAddress, int Actor, uint DescriptorAddress, int RandomDivisor) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66ForceCamera50A730 : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66ModelMask7C(int Actor, uint Mask) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66SetMode4A8480(int Mode) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Flags(uint AndMask, uint OrMask) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Sequence505C00(int Actor, byte Sequence) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66MusicFade46BBC0(uint Channel, byte Duration, int Gain) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66MusicStop46B800(uint Channel) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66UploadTim505E30(uint SourceAddress) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Text4876F0(int Actor, int Text, int Duration) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Flash5712C0(int Rise, int Hold, int Fall, int Intensity) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Stage5713E0(int Intensity, int Selector) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Rectangle571250(int Red, int Green, int Blue) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66PcRender47CF50(int Mode, int Red, int Green, int Blue, int Alpha) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66Contact506690(int ContactIndex) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66StopVibration4A2940(int Handle) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66SoundGain46BEC0(int Duration, int Gain) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66CaptureCameraRetirement : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66StopSound46B450(int Voice, int Mode) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect66Operation;

public sealed record NativeBattleEffect66PublishCameraPose(NativeBattleCameraPose Pose) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66DrawBody176(uint TemporaryAddress) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66DrawStageBody44(uint TemporaryAddress) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66ModelFlags(int Actor, ushort AndMask, ushort OrMask) : NativeBattleEffect66Operation;
public sealed record NativeBattleEffect66StageGroupFlags(byte AndMask, byte OrMask) : NativeBattleEffect66Operation;
