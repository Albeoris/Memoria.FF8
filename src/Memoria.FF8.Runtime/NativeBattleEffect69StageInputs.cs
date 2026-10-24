using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect69StageSource(NativeBattleStageGroup Group, ImmutableArray<NativeBattleRawMesh> RawGeometry, uint ScratchAddress, int ScratchLength)
{
    public uint GeometryAddress { get; init; }
}

public sealed record NativeBattleEffect69StageStep(NativeBattleEffect69Frame Frame, uint NextMetric);

public sealed partial record NativeBattleEffect69Inputs
{
    public Func<NativeBattleEffect69Frame, int, NativeBattleEffect69StageSource>? ReadStage { get; init; }
    public Func<NativeBattleEffect69Frame, int, NativeBattlePackedMatrix32?, NativeBattleEffect69Frame>? PrepareStage { get; init; }
}

public sealed record NativeBattleEffect69DeliverCommonBatch(NativeBattleGfGeometryBatch Batch) : NativeBattleEffect69Operation;
