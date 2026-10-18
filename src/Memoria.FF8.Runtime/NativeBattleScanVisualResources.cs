using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanVisualResources(ImmutableArray<NativeBattleScanReticlePoint> Trajectory15D1898)
{
    public required ImmutableDictionary<uint, NativeBattleStaticMeshResource> MeshesByAddress { get; init; }
    public required ImmutableDictionary<uint, ImmutableArray<byte>> VertexBanksByAddress { get; init; }
    public required ImmutableDictionary<uint, ImmutableArray<byte>> TablesByAddress { get; init; }
    public required ImmutableArray<ImmutableArray<byte>> LineArrays15D1718 { get; init; }
    public required NativeBattleModuleResource Sprite15D07D4 { get; init; }
    public required NativeBattleModuleResource Sprite15D0AF8 { get; init; }
}
