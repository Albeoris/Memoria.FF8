using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87Resources(NativeBattleRinoa87IoResources Io, NativeBattleEffectOwnedModelResource Model, uint DefaultColor44, NativeBattleEffectHeapAllocation ActorOverride, NativeBattleEffectHeapAllocation SceneTim, NativeBattleRinoa87CameraResources Camera, NativeBattleRinoa87NodeResources Nodes, NativeBattleGfAnimationSource Program)
{
    public ImmutableDictionary<uint, ImmutableArray<byte>> Meshes { get; init; } = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty;
    public ImmutableArray<byte> Sprite { get; init; }
    public ImmutableArray<short> TargetCurve { get; init; }
    public ImmutableArray<short> FirstImpactCounts { get; init; }
    public ImmutableArray<short> SecondImpactCounts { get; init; }
    public ImmutableDictionary<uint, uint> Sounds { get; init; } = ImmutableDictionary<uint, uint>.Empty;
}
