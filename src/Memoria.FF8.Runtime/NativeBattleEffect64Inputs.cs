namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect64Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect64State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect64Step(NativeBattleEffect64Frame Frame, int Result);
public sealed record NativeBattleEffect64PointStep(NativeBattleEffect64Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect64Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect64Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect64Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect64Frame, short> ReadCameraRoll, Func<NativeBattleEffect64Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect64Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect64PointStep> ReadBone502170, Func<NativeBattleEffect64Frame, NativeBattleEffect64Operation, NativeBattleEffect64Frame> Execute);

public abstract record NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64UploadTim505E30(uint SourceAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ModelFlags(uint ModelAddress, ushort AndMask, ushort OrMask) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ModelMeshMask(uint ModelAddress, uint Mask) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64StopVibration4A2940(int Handle) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ApplyOffsetPose662C00(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64RestorePose508C90(uint ModelAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ClearGeometryBit15_6E5C90(uint ModelAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64DrawBody6E56A0(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect64Operation;
