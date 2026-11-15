namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect64Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect64State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect64Step(NativeBattleEffect64Frame Frame, int Result);
public sealed record NativeBattleEffect64PointStep(NativeBattleEffect64Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect64Inputs(NativeBattleModelAliases Aliases, NativeBattleSummonColors Colors, Func<NativeBattleEffect64Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect64Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect64Frame, short> ReadCameraRoll, Func<NativeBattleEffect64Frame, BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect64Frame, BattleModelOwner, int, int, NativeBattleEffectArena, NativeBattleEffect64PointStep> ReadBone502170, Func<NativeBattleEffect64Frame, NativeBattleEffect64Operation, NativeBattleEffect64Frame> Execute);

public abstract record NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64UploadTim505E30(uint SourceAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ModelFlags(BattleModelOwner Model, ushort AndMask, ushort OrMask) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64PartyFlags(ushort AndMask, ushort OrMask) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ModelMeshMask(BattleModelOwner Model, uint Mask) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64StopVibration4A2940(int Handle) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ApplyOffsetPose662C00(BattleModelOwner Model, uint OffsetRowsAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64RestorePose508C90(BattleModelOwner Model) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64ClearGeometryBit15_6E5C90(BattleModelOwner Model) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64DrawBody6E56A0(BattleModelOwner Model, uint OffsetRowsAddress) : NativeBattleEffect64Operation;
public sealed record NativeBattleEffect64DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect64Operation;
