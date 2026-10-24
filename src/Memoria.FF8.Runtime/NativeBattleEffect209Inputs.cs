namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect209Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect209State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect209Step(NativeBattleEffect209Frame Frame, int Result);
public sealed record NativeBattleEffect209PointStep(NativeBattleEffect209Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect209Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect209Frame, uint> ReadFlags, Func<NativeBattleEffect209Frame, NativeBattleEffect209Step> ReadCurrentStep508500, Func<NativeBattleEffect209Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect209Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect209Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect209Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect209Frame, short> ReadCameraRoll, Func<NativeBattleEffect209Frame, short> ReadScalarFocal, Func<NativeBattleEffect209Frame, byte> ReadBuffer80, Func<NativeBattleEffect209Frame, uint, uint> ReadColor, Func<NativeBattleEffect209Frame, int, NativeBattleEffectArena, NativeBattleEffect209PointStep> Anchor571400, Func<NativeBattleEffect209Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect209PointStep> Point502170, Func<NativeBattleEffect209Frame, int, bool> ReadVibrationPlaying4A2900, Func<NativeBattleEffect209Frame, NativeBattleEffect209Operation, NativeBattleEffect209Frame> Execute);

public abstract record NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209UploadTim505E30(uint SourceAddress) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209ModelFlags(int Actor, ushort AndMask, ushort OrMask, bool WordWidth) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209QueueVariant50E3A0(uint BufferAddress) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209RebuildVariant50E3C0(uint BufferAddress) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209Stage5713E0(int Intensity, uint Color) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209Contact506690(int Index) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209StopVibration4A2940(int Handle) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209Flash5712C0(int Rise, int Hold, int Fall, byte Color) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209SetViewTranslationY(short Value) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209CommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209SetProjectionH(ushort Value) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209DrawAddressedTemplate(uint Address, NativeBattleEffectArena Data40, int Bucket, ushort Flags = 1) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209CaptureStage7D30 : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209SetStageFlags2(bool Enabled) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209PublishColor(uint Address, uint Color) : NativeBattleEffect209Operation;
public sealed record NativeBattleEffect209PublishNormalColor44(uint Color) : NativeBattleEffect209Operation;
