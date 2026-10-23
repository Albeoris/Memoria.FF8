namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShotgun281Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleShotgun281State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleShotgun281PointStep(NativeBattleShotgun281Frame Frame, NativeBattleEffectArena Point8, int Result);
public sealed record NativeBattleShotgun281Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleShotgun281Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleShotgun281Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleShotgun281Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleShotgun281Frame, uint, int> ReadBoneCount, Func<NativeBattleShotgun281Frame, uint, int, short> ReadBoneParent, Func<NativeBattleShotgun281Frame, uint, int, byte> ReadBoneAlias, Func<NativeBattleShotgun281Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleShotgun281Frame, uint, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleShotgun281Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleShotgun281Frame, NativeBattleShotgun281Operation, NativeBattleShotgun281Frame> Execute);

public abstract record NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281ComposeCaster5095B0(uint ModelAddress) : NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281RestoreCaster508C90(uint ModelAddress) : NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281Contact506690(int ContactIndex) : NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281Flash5712C0(int Arg0, int Arg1, int Arg2, int Arg3) : NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleShotgun281Operation;
public sealed record NativeBattleShotgun281DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleShotgun281Operation;
