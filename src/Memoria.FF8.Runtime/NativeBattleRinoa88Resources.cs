using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88Resources(NativeBattleRinoa88IoResources Io, NativeBattleEffectOwnedModelResource PrimaryModel, NativeBattleEffectOwnedModelResource SecondaryModel, uint DefaultColor44, NativeBattleEffectHeapAllocation ActorOverride, ImmutableDictionary<uint, NativeBattleEffectHeapAllocation> SceneTextures, NativeBattleRinoa88CameraResources Camera, NativeBattleGfAnimationSource Program, NativeBattleRinoa88NodeResources Nodes)
{
    public ImmutableDictionary<uint, ImmutableArray<byte>> Sprites { get; init; } = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty;
    public ImmutableDictionary<uint, ImmutableArray<byte>> Meshes { get; init; } = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty;
    public ImmutableArray<uint> ScreenColors { get; init; }
    public ImmutableDictionary<uint, uint> Sounds { get; init; } = ImmutableDictionary<uint, uint>.Empty;
}
