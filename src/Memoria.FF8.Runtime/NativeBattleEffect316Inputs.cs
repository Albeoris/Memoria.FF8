namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect316Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect316State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect316Step(NativeBattleEffect316Frame Frame, int Result);
public sealed record NativeBattleEffect316AddressStep(NativeBattleEffect316Frame Frame, uint Address);
public sealed record NativeBattleEffect316Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect316Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect316Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect316Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect316Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect316Frame, short> ReadCameraRoll, Func<NativeBattleEffect316Frame, uint, int, NativeBattleEffectArena> ReadArena, Func<NativeBattleEffect316Frame, uint, uint> ReadGeometryAddress, Func<NativeBattleEffect316Frame, uint, int, uint> ResolveBoneRecord, Func<NativeBattleEffect316Frame, int, NativeBattleEffect316AddressStep> AllocateTemporary, Func<NativeBattleEffect316Frame, int, NativeBattleEffect316Frame> ReleaseTemporary, Func<NativeBattleEffect316Frame, NativeBattleEffect316Operation, NativeBattleEffect316Frame> Execute);

public abstract record NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316UploadTim505E30(uint SourceAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316WriteModel(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316WriteArena(uint Address, NativeBattleEffectArena Data, int Length) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316ClearGeometryMarkers6612C0(uint ModelAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Shadow5088A0(uint ModelAddress, int OrderingBucket, int Count) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Prepare5095B0(uint ModelAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Restore508C90(uint ModelAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316PublishCursors(uint CommonCursor, uint PrivateCursor) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Blend56CC00(uint Source1Address, uint Source2Address, int Weight1, int Weight2, uint DestinationAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Stage5713E0(int Amount, uint Flags) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Music47E3C0(int Duration) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Vibration4A29A0(uint SourceAddress, int Mode, int Magnitude, uint OutputAddress) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316StopVibration4A2940(int Handle) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Draw661550(uint ModelAddress, int X, int Y, int Z) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316Draw661CC0(uint MainAddress, uint ModelAddress, uint ProjectedRowsAddress, int X, int Y, int Z) : NativeBattleEffect316Operation;
public sealed record NativeBattleEffect316AdvanceRecords662720(uint MainAddress) : NativeBattleEffect316Operation;
