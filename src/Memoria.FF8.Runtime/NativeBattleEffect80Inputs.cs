namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect80Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect80State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect80Step(NativeBattleEffect80Frame Frame, int Result);
public sealed record NativeBattleEffect80Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect80Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect80Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect80Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect80Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect80Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect80Frame, int, NativeBattleEffectArena> ReadPosition8, Func<NativeBattleEffect80Frame, short> ReadCameraRoll, Func<NativeBattleEffect80Frame, uint, uint> ReadColor, Func<NativeBattleEffect80Frame, NativeBattleEffect80Operation, NativeBattleEffect80Frame> Execute);

public abstract record NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80Point502170(uint ModelAddress, ushort Selector, uint OutputAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80UploadTim(uint Address) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80Contact506690(int ContactIndex) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80TextureLoad508630(uint SourceAddress, uint CompletionByteAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80TargetFlags(uint ModelAddress, ushort AndMask, ushort OrMask, bool WordWidth, byte ByteOffset = 0) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80StageTransition(int Index, ushort Intensity, bool ClearRgb) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80CopyActorPoint(uint DestinationModel, uint SourceModel, short YOffset, bool PublishWorld) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80SetActorPosition(uint ModelAddress, NativeBattleVector Position, bool PublishWorld) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80SetSequence505C00(uint ModelAddress, byte Sequence) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80StopVibration4A2940(short Handle) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80BodyAnchor7B4750(uint TaskAddress, uint ContextAddress) : NativeBattleEffect80Operation;
public sealed record NativeBattleEffect80DrawBody7AFEC0(uint ContextAddress) : NativeBattleEffect80Operation;
