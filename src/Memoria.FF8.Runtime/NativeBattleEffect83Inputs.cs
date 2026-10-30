namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect83State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect83Step(NativeBattleEffect83Frame Frame, int Result);
public sealed record NativeBattleEffect83Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect83Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect83Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect83Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect83Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect83Frame, int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect83Frame, short> ReadCameraRoll, Func<NativeBattleEffect83Frame, uint, uint> ReadColor, Func<NativeBattleEffect83Frame, NativeBattleEffect83Operation, NativeBattleEffect83Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83Point502170(uint ModelAddress, ushort Selector, uint OutputAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83UploadTim(uint Address) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83Contact506690(int ContactIndex) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83TextureLoad508630(uint SourceAddress, uint CompletionByteAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83TargetCapture79E590(uint TaskAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83TargetDraw79DA20(uint BodyAddress) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83TargetFlags(uint ModelAddress, ushort AndMask, ushort OrMask, bool WordWidth) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83StageTransition(int Index, ushort Intensity, bool ClearRgb) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83PublishMatrix1D99AB0(NativeBattlePackedMatrix32 Matrix) : NativeBattleEffect83Operation;
public sealed record NativeBattleEffect83DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect83Operation;
