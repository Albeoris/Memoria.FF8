namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMegaSpark279Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMegaSpark279State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    public NativeBattleStackBytes? StackBytes { get; init; }
}

public sealed record NativeBattleMegaSpark279PointStep(NativeBattleMegaSpark279Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleMegaSpark279Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<NativeBattleMegaSpark279Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleMegaSpark279Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleMegaSpark279Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleMegaSpark279Frame, uint, int> ReadBoneCount, Func<NativeBattleMegaSpark279Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleMegaSpark279Frame, uint, int, int, NativeBattleMegaSpark279PointStep> ReadBone502170, Func<NativeBattleMegaSpark279Frame, uint, NativeBattleMegaSpark279PointStep> ReadAnchor571400, Func<NativeBattleMegaSpark279Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleMegaSpark279Frame, NativeBattleMegaSpark279Operation, NativeBattleMegaSpark279Frame> Execute)
{
    public Func<NativeBattleMegaSpark279Frame, short>? ReadRowPointW { get; init; }
}

public abstract record NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279ComposeCaster5095B0(uint ModelAddress) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279RestoreCaster508C90(uint ModelAddress) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279ModelFields(uint ModelAddress, ushort ClearFlags0, ushort SetFlags0, short? Rotation12 = null, short? PositionZ = null) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279Contact506BA0(int FirstContactIndex, int Count) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279Flash5712C0(int Arg0, int Arg1, int Arg2, int Arg3) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleMegaSpark279Operation;
public sealed record NativeBattleMegaSpark279DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleMegaSpark279Operation;
