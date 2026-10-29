using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleStorageRestoreContext
{
    private readonly Dictionary<int, NativeBattleWorkingMemory> graphOwners = [];
    private readonly Dictionary<(int AuthorityId, bool Borrowed), NativeBattleEffectArena> graphDefiningViews = [];
    private bool hasStorageGraph;

    internal void PrepareStorageGraph(BattleSnapshot snapshot) => PrepareStorageGraph(snapshot, snapshot.StorageGraph);
    internal void PrepareStorageGraph(NativeBattleRetainedMemory snapshot) => PrepareStorageGraph(snapshot, snapshot.StorageGraph);

    private void PrepareStorageGraph(object snapshot, NativeBattleStorageGraph? storageGraph)
    {
        if (storageGraph is not { } graph)
        {
            return;
        }
        if (graph.Authorities.IsDefault)
        {
            throw new InvalidOperationException("Battle ownership metadata requires an authority list.");
        }
        hasStorageGraph = true;
        NativeBattleEffectArena[] views = NativeBattleStorageGraphViews.Enumerate(snapshot).Distinct<NativeBattleEffectArena>(ReferenceEqualityComparer.Instance).ToArray();
        Dictionary<int, NativeBattleStorageAuthority> definitions = [];
        foreach (NativeBattleStorageAuthority authority in graph.Authorities)
        {
            if (authority.Id <= 0 || authority.LogicalLength < 0 || authority.LogicalLength > NativeBattleEffectArena.Length || authority.StoredLength < 0 || authority.StoredLength > authority.LogicalLength || authority.DataFootprint.IsDefault || authority.ClassifiedRanges.IsDefault || !definitions.TryAdd(authority.Id, authority))
            {
                throw new InvalidOperationException("Battle ownership metadata contains an invalid authority.");
            }
        }
        Dictionary<int, NativeBattleEffectArena> definingViews = [];
        HashSet<int> referenced = [];
        foreach (NativeBattleEffectArena view in views)
        {
            if (view.StorageReference is not { } reference)
            {
                continue;
            }
            if (!view.IsSnapshot || !definitions.TryGetValue(reference.AuthorityId, out NativeBattleStorageAuthority? authority) || reference.Offset < 0 || reference.Length < 0 || reference.Length != view.LogicalLength || reference.Offset > authority.LogicalLength - reference.Length)
            {
                throw new InvalidOperationException("Battle storage view references an invalid physical range.");
            }
            referenced.Add(reference.AuthorityId);
            if (reference.DefinesAuthority && (reference.Offset != 0 || reference.Length != authority.LogicalLength || authority.Content is not null))
            {
                throw new InvalidOperationException("Battle physical authority has conflicting definitions.");
            }
            if (reference.DefinesAuthority)
            {
                definingViews.TryAdd(reference.AuthorityId, view);
            }
        }
        foreach ((int id, NativeBattleStorageAuthority authority) in definitions)
        {
            if (!referenced.Contains(id))
            {
                throw new InvalidOperationException("Battle ownership metadata contains an unreferenced authority.");
            }
            NativeBattleStorageAuthorityContent content;
            if (authority.Content is { } stored)
            {
                content = stored;
            }
            else if (definingViews.TryGetValue(id, out NativeBattleEffectArena? definition))
            {
                ValidateDefinitionCoverage(definition, authority);
                content = ExtractContent(definition, authority.StoredLength);
            }
            else
            {
                throw new InvalidOperationException("Battle physical authority is missing its data definition.");
            }
            NativeBattleWorkingMemorySnapshot frozen = new(authority.LogicalLength, authority.InitialKnownZero, authority.StoredLength, content.Regions, content.UnknownRegions, content.ResourcePointers, authority.DataFootprint, authority.ClassifiedRanges, content.TextureFiles);
            NativeBattleWorkingMemory owner = NativeBattleWorkingMemory.Restore(frozen);
            graphOwners.Add(id, owner);
            foreach (NativeBattleEffectArena view in views)
            {
                if (view.StorageReference?.AuthorityId == id && view.CapturedAuthority is { } captured)
                {
                    GetOrAddOwner(captured, () => owner);
                }
            }
        }
        foreach (NativeBattleEffectArena view in views)
        {
            if (view.StorageReference is { } reference)
            {
                ValidateView(view, graphOwners[reference.AuthorityId], reference);
            }
        }
    }

    internal bool TryRestoreGraphView(NativeBattleEffectArena snapshot, out NativeBattleEffectArena? restored)
    {
        if (hasStorageGraph && snapshot.StorageReference is { } reference)
        {
            if (!graphOwners.TryGetValue(reference.AuthorityId, out NativeBattleWorkingMemory? owner))
            {
                throw new InvalidOperationException("Battle storage references an unknown physical authority.");
            }
            if (reference.DefinesAuthority)
            {
                // PrepareStorageGraph validates every serialized view before mapping begins.
                // Repeated definitions represent one full arena object, even after JSON has duplicated its DTO.
                (int AuthorityId, bool Borrowed) key = (reference.AuthorityId, reference.Borrowed);
                if (!graphDefiningViews.TryGetValue(key, out NativeBattleEffectArena? definition))
                {
                    definition = NativeBattleEffectArena.RestoreGraphView(owner, reference);
                    graphDefiningViews.Add(key, definition);
                }
                restored = definition;
            }
            else
            {
                restored = NativeBattleEffectArena.RestoreGraphView(owner, reference);
            }
            return true;
        }
        restored = null;
        return false;
    }

    internal static void ValidateDefinitionCoverage(NativeBattleEffectArena definition, NativeBattleStorageAuthority authority)
    {
        int index = 0;
        foreach (NativeBattleEffectUnknownRegion range in authority.DataFootprint)
        {
            if (range.Length == 0)
            {
                continue;
            }
            if (index >= definition.Regions.Length || definition.Regions[index].Offset != range.Offset || definition.Regions[index].Data.Length != range.Length)
            {
                throw new InvalidOperationException("Battle physical data footprint disagrees with its canonical arena definition.");
            }
            index++;
        }
        if (index != definition.Regions.Length)
        {
            throw new InvalidOperationException("Battle physical data footprint omits canonical arena regions.");
        }
    }

    internal static NativeBattleStorageAuthorityContent ExtractContent(NativeBattleEffectArena arena, int storedLength)
    {
        ImmutableArray<NativeBattleEffectMemoryRegion>.Builder regions = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
        foreach (NativeBattleEffectMemoryRegion region in arena.Regions)
        {
            int length = Math.Min(region.Data.Length, Math.Max(0, storedLength - region.Offset));
            if (length > 0)
            {
                regions.Add(length == region.Data.Length ? region : new(region.Offset, ImmutableArray.CreateRange(region.Data.AsSpan(0, length).ToArray())));
            }
        }
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        foreach (NativeBattleEffectUnknownRegion range in arena.UnknownRegions)
        {
            int length = Math.Min(range.Length, Math.Max(0, storedLength - range.Offset));
            if (length > 0)
            {
                unknown.Add(new(range.Offset, length));
            }
        }
        return new(regions.ToImmutable(), unknown.ToImmutable(), arena.ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty, arena.TextureFiles);
    }

    private static void ValidateView(NativeBattleEffectArena view, NativeBattleWorkingMemory owner, NativeBattleStorageReference reference)
    {
        view.Validate();
        Span<byte> actual = stackalloc byte[256];
        Span<byte> expected = stackalloc byte[256];
        Span<bool> actualKnown = stackalloc bool[256];
        Span<bool> expectedKnown = stackalloc bool[256];
        for (int offset = 0; offset < reference.Length;)
        {
            int length = Math.Min(actual.Length, reference.Length - offset);
            view.ReadWithKnownness(offset, actual[..length], actualKnown[..length]);
            owner.ReadWithKnownness(reference.Offset + offset, expected[..length], expectedKnown[..length]);
            if (!actual[..length].SequenceEqual(expected[..length]) || !actualKnown[..length].SequenceEqual(expectedKnown[..length]))
            {
                throw new InvalidOperationException("Battle storage view data disagrees with its physical authority.");
            }
            offset += length;
        }
        ImmutableArray<NativeBattleTextureFileSource> expectedFiles = NativeBattleTextureFileMetadata.Project(owner.TextureFiles, reference.Offset, reference.Length) ?? [];
        if (!(view.TextureFiles ?? []).SequenceEqual(expectedFiles))
        {
            throw new InvalidOperationException("Battle storage view texture files disagree with its physical authority.");
        }
        ImmutableDictionary<int, NativeBattleResourcePointer?> pointers = view.ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty;
        int count = 0;
        using Dictionary<int, NativeBattleResourcePointer?>.Enumerator entries = owner.EnumerateResourcePointers();
        while (entries.MoveNext())
        {
            KeyValuePair<int, NativeBattleResourcePointer?> pointer = entries.Current;
            if (pointer.Key < reference.Offset || pointer.Key > reference.Offset + reference.Length - 4)
            {
                continue;
            }
            count++;
            if (!pointers.TryGetValue(pointer.Key - reference.Offset, out NativeBattleResourcePointer? actualPointer) || actualPointer != pointer.Value)
            {
                throw new InvalidOperationException("Battle storage view provenance disagrees with its physical authority.");
            }
        }
        if (pointers.Count != count)
        {
            throw new InvalidOperationException("Battle storage view contains unrelated pointer provenance.");
        }
    }
}
