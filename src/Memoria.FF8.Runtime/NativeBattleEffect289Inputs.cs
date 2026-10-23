namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect289Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect289State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect289PointStep(NativeBattleEffect289Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleEffect289Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect289Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect289Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect289Frame, uint, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect289Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect289Frame, uint, NativeBattleEffect289PointStep> ReadAnchor571400, Func<NativeBattleEffect289Frame, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect289PointStep> ReadCasterPoint678390, Func<NativeBattleEffect289Frame, NativeBattleEffect289Operation, NativeBattleEffect289Frame> Execute, Func<NativeBattleEffect289Frame, NativeBattleGfAnimationSource, NativeBattleGfAnimationNode, NativeBattleEffectArena, NativeBattleEffect289Frame> DrawProgrammeNode);

public abstract record NativeBattleEffect289Operation;
public sealed record NativeBattleEffect289Contact506690 : NativeBattleEffect289Operation;
public sealed record NativeBattleEffect289Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleEffect289Operation;
public sealed record NativeBattleEffect289DrawCustom108(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect289Operation;
public sealed record NativeBattleEffect289DrawMesh7043B0(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect289Operation;
public sealed record NativeBattleEffect289DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleEffect289Operation;
public sealed record NativeBattleEffect289DeliverBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect289Operation;
