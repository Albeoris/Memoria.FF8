namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect79State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect79Step(NativeBattleEffect79Frame Frame, int Result);
public sealed partial record NativeBattleEffect79Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect79Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect79Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect79Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect79Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect79Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect79Frame, short> ReadCameraRoll, Func<NativeBattleEffect79Frame, uint, uint> ReadColor, Func<NativeBattleEffect79Frame, NativeBattleEffect79Operation, NativeBattleEffect79Frame> Execute)
{
    public bool CapturePresentation { get; init; }
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public abstract record NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79TargetFlags(uint ModelAddress, ushort AndMask, ushort OrMask, bool WordWidth) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79UploadTim(uint Address) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79QueueFile48D0A0(ushort FileId, uint Destination, uint Callback) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79StageMusic501A20(uint SourceAddress, uint CompletionByteAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79MusicGain501B10(int Duration, int Gain) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79MusicFade46BBC0(int Channel, int Duration, int Gain) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79StartVibration4A29A0(uint BankAddress, int Flags, int Power, uint HandleOutputAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79StopVibration4A2940(short Handle) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79SoundBank501860(uint SourceAddress, uint CompletionByteAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79TextureLoad508630(uint SourceAddress, uint CompletionByteAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79Contact506690(int ContactIndex) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79ObserveCasterAnchor(uint TaskAddress, uint ContextAddress) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79DrawBody7BD020(uint ContextAddress) : NativeBattleEffect79Operation
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public sealed record NativeBattleEffect79ModelPatch(int Actor, NativeBattleVector? Position = null, NativeBattleVector? WorldTranslation = null, uint? ScalePointer = null, uint? Color40 = null, byte? ColorMode43 = null, byte? Alpha7 = null, byte? Sequence = null) : NativeBattleEffect79Operation;
public sealed record NativeBattleEffect79Sound501330(uint SourceAddress, int Flags, int Pan) : NativeBattleEffect79Operation;

public sealed record NativeBattleEffect79ObserveBodyStack(uint TaskAddress, bool DrewBody) : NativeBattleEffect79Operation;

public sealed record NativeBattleEffect79ObserveNodeStack(uint Instruction, uint? FirstDword) : NativeBattleEffect79Operation;
