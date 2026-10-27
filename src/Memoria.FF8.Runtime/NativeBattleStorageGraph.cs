using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Identifies one bounded view of a physical owner in a detached battle graph.</summary>
public sealed record NativeBattleStorageReference(int AuthorityId, int Offset, int Length, bool DefinesAuthority = false, bool Borrowed = false);

/// <summary>Optional ownership metadata; ordinary arena data remains in its existing snapshot fields.</summary>
public sealed record NativeBattleStorageGraph(ImmutableArray<NativeBattleStorageAuthority> Authorities);

public sealed record NativeBattleStorageAuthority(int Id, int LogicalLength, bool InitialKnownZero, int StoredLength, ImmutableArray<NativeBattleEffectUnknownRegion> DataFootprint, ImmutableArray<NativeBattleEffectUnknownRegion> ClassifiedRanges)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStorageAuthorityContent? Content { get; init; }
}

public sealed record NativeBattleStorageAuthorityContent(ImmutableArray<NativeBattleEffectMemoryRegion> Regions, ImmutableArray<NativeBattleEffectUnknownRegion> UnknownRegions, ImmutableDictionary<int, NativeBattleResourcePointer?> ResourcePointers);

internal interface INativeBattleArenaStorageNode
{
    NativeBattleEffectArena? SnapshotArena { get; }
}
