using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private sealed class WorldAllocationCache
    {
        public long Generation { get; set; } = -1;
        public byte[]? Program { get; set; }
        public Dictionary<ushort, NativeWorldAllocationClause> Clauses { get; } = [];
    }

    private static readonly ConditionalWeakTable<ResourceService, WorldAllocationCache> WorldAllocationCaches = new();
    private readonly WorldAllocationCache worldAllocationCache = new();

    public NativeWorldAllocationClause ReadNativeWorldAllocationClause(ushort story)
    {
        WorldAllocationCache cache = overrides is null ? WorldAllocationCaches.GetValue(resources, _ => new WorldAllocationCache()) : worldAllocationCache;
        long generation = resources.Generation;
        lock (cache)
        {
            if (cache.Program is null || cache.Generation != generation || overrides is not null)
            {
                using IFlArchive world = OpenArchive("world");
                byte[] wmset = Read(world, "wmsetus.obj");
                int[] sections = WorldAssetReader.ReadSections(wmset);
                cache.Program = wmset[sections[9]..sections[10]];
                cache.Generation = generation;
                cache.Clauses.Clear();
            }
            if (!cache.Clauses.TryGetValue(story, out NativeWorldAllocationClause? clause))
            {
                clause = NativeWorldAllocationClauseReader.Read(cache.Program, story);
                cache.Clauses.Add(story, clause);
            }
            return clause;
        }
    }
}
