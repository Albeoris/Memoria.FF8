namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect167Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect167State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect167Step(NativeBattleEffect167Frame Frame, int Result);
public sealed record NativeBattleEffect167PointStep(NativeBattleEffect167Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect167Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect167Frame, uint> ReadFlags, Func<NativeBattleEffect167Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect167Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect167Frame, int, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect167Frame, int, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix32, Func<NativeBattleEffect167Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect167Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect167Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect167Frame, short> ReadScalarFocal, Func<NativeBattleEffect167Frame, short> ReadCameraRoll, Func<NativeBattleEffect167Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect167PointStep> Point502170, Func<NativeBattleEffect167Frame, int, int, int, NativeBattleEffectArena, NativeBattleEffect167PointStep> LocalBonePoint3B20, Func<NativeBattleEffect167Frame, NativeBattleEffect167Operation, NativeBattleEffect167Frame> Execute)
{
    public bool CapturePresentation { get; init; }
    public Func<NativeBattleEffect167Frame, uint, uint>? ReadColor { get; init; }
}

public abstract record NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167UploadTim505E30(uint SourceAddress) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167ForceCamera50A730 : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167SetProjectionH(ushort Value) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167Flash5712C0(int Rise, int Hold, int Fall, byte Color) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167StageGroupFlags(int Group, byte AndMask, byte OrMask) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167WriteModelBytes(int Actor, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167ResourceFlags44(int Actor, ushort AndMask, ushort OrMask) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167AllContacts506BA0(int FirstContactIndex, int Count) : NativeBattleEffect167Operation;
public sealed record NativeBattleEffect167DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect167Operation;
