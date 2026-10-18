using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundagaFragmentReference(uint Address, ImmutableArray<byte> Bytes);
public sealed record NativeBattleThundagaFragmentPair(NativeBattleStaticMeshResource MutableMesh, NativeBattleThundagaFragmentReference Reference);

/// <summary>Original source seeds; mutable fragment and morph work bytes belong to each retained execution owner.</summary>
public sealed record NativeBattleThundaga105Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleGfAnimationSource ProgramSource, NativeBattleModuleResource SpriteStationary, NativeBattleModuleResource SpriteMoving, NativeBattleEffectHeapAllocation SoundDescriptor, NativeBattleEffectHeapAllocation NodeSelector, ImmutableArray<NativeBattleThundagaFragmentPair> FragmentPairs)
{
    [JsonIgnore] public uint Sound => BinaryPrimitives.ReadUInt32LittleEndian(SoundDescriptor.Data.AsSpan());
}
