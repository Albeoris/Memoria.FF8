namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStare45Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleStare45State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleStare45PointStep(NativeBattleStare45Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleStare45Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleStare45Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleStare45Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleStare45Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleStare45Frame, int, int, int, NativeBattleStare45PointStep> ReadBone502170, Func<NativeBattleStare45Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleStare45Frame, NativeBattleStare45Operation, NativeBattleStare45Frame> Execute);

public abstract record NativeBattleStare45Operation;
public sealed record NativeBattleStare45UpdateCaster8DC740(uint TaskAddress) : NativeBattleStare45Operation;
public sealed record NativeBattleStare45UpdateTargetBounds8DC610_8DC870(uint TaskAddress) : NativeBattleStare45Operation;
public sealed record NativeBattleStare45Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleStare45Operation;
public sealed record NativeBattleStare45Contact506690(int ContactIndex) : NativeBattleStare45Operation;
public sealed record NativeBattleStare45StageColorDepth(int Group, short Depth) : NativeBattleStare45Operation;
public sealed record NativeBattleStare45StageRgb(int Group, byte R, byte G, byte B) : NativeBattleStare45Operation;
public sealed record NativeBattleStare45DrawMesh104(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleStare45Operation;
