namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGastricJuice128Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleGastricJuice128State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}


public abstract record NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128SnapshotBody(byte Actor, uint CacheAddress) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128AdvanceAct(uint TaskAddress) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128DrawBody132(byte Actor, short Fraction) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128DeliverRibbonBatch(NativeBattleGfGeometryBatch Batch) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128ActorFlags(byte Actor, ushort ClearMask, ushort SetMask) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128Sound(uint Value, int Pan) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128Contact(int Index) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, byte DepthShift = 2) : NativeBattleGastricJuice128Operation;
public sealed record NativeBattleGastricJuice128PointStep(NativeBattleGastricJuice128Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleGastricJuice128AnchorStep(NativeBattleGastricJuice128Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleGastricJuice128Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, int> ReadBoneCount, Func<NativeBattleGastricJuice128Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleGastricJuice128Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleGastricJuice128Frame, int, NativeBattleGastricJuice128AnchorStep> ReadAnchor571400, Func<NativeBattleGastricJuice128Frame, int, short, short, NativeBattleGastricJuice128PointStep> ReadBone502170, Func<NativeBattleGastricJuice128Frame, NativeBattleGastricJuice128Operation, NativeBattleGastricJuice128Frame> Execute);
