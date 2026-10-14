using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record WorldGroundSelection(NativeWorldGroundIdentity Identity, WorldGround Ground);
public sealed record WorldNativeGroundResult(WorldGroundSelection? Center, NativeWorldGroundCacheState Cache, int X, int Z, int GroundYaw, bool Blocked);

public sealed partial class WorldAssets
{
    private readonly Dictionary<NativeWorldGroundIdentity, WorldTriangle> nativeGroundPolygons = [];
    private sealed class NativeGroundProbe(int x, int z)
    {
        public int X { get; } = (int)Wrap(x, 262144);
        public int Z { get; } = (int)Wrap(z, 196608);
        public int Cell => NativeCell(X, Z);
        public int Status { get; set; } = 2;
        public WorldGroundSelection? Selected { get; set; }
    }

    public WorldGroundSelection? LookupNativeGround(int x, int z, IReadOnlyDictionary<int, int> replacements, IReadOnlyList<NativeWorldGroundIdentity> cache)
    {
        x = (int)Wrap(x, 262144);
        z = (int)Wrap(z, 196608);
        int cell = NativeCell(x, z);
        foreach (NativeWorldGroundIdentity identity in cache)
        {
            if (identity.Cell == cell && TrySelected(identity, x, z, replacements, out WorldGroundSelection? selected))
            {
                return selected;
            }
        }
        foreach (WorldGroundSelection selected in ContainingNativeGround(cell, x, z, replacements))
        {
            // Native53EB80 is a lookup, not a cache insertion or permission/nearest-height search.
            return selected;
        }
        return null;
    }

    public WorldTriangle ResolveNativeGroundIdentity(NativeWorldGroundIdentity identity, IReadOnlyDictionary<int, int> replacements)
    {
        identity.Validate();
        int expectedSource = replacements.TryGetValue(identity.MapId, out int replacement) ? replacement : identity.MapId;
        if (identity.SourceId != expectedSource)
        {
            throw new InvalidDataException("Native cached ground source differs from the current original replacement selection.");
        }
        if (nativeGroundPolygons.TryGetValue(identity, out WorldTriangle? cached))
        {
            return cached;
        }
        WorldSegment segment = GetSegment(identity.MapId, replacements);
        if (segment.SourceId != identity.SourceId)
        {
            throw new InvalidDataException("Native cached ground source differs from the current original replacement selection.");
        }
        WorldTriangle triangle = segment.Triangles.SingleOrDefault(triangle => triangle.NativeBlockIndex == identity.BlockIndex && triangle.NativePolygonIndex == identity.PolygonIndex) ?? throw new InvalidDataException("Native cached original polygon is absent.");
        nativeGroundPolygons.Add(identity, triangle);
        return triangle;
    }

    public void ValidateNativeGroundCache(NativeWorldGroundCacheState state, IReadOnlyDictionary<int, int> replacements)
    {
        state.Validate();
        foreach (NativeWorldGroundAlternative alternative in state.Alternatives)
        {
            _ = ResolveNativeGroundIdentity(alternative.CurrentGround, replacements);
            _ = ResolveNativeGroundIdentity(alternative.PreviousGround, replacements);
            foreach (NativeWorldGroundIdentity entry in alternative.Entries)
            {
                _ = ResolveNativeGroundIdentity(entry, replacements);
            }
        }
    }

    public WorldNativeGroundResult ResolveNativeGround(int beforeX, int beforeZ, int moveX, int graphicsMoveZ, int previousGroundHeight, int? selectedGroup, NativeWorldGroundCacheState prior, IReadOnlyDictionary<int, int> replacements, byte entityType = 33, bool selectionMayRetainGroup = false)
    {
        bool foot = entityType < 10 || entityType == 128;
        bool car = entityType is >= 32 and <= 40 or 132;
        if (!foot && !car || selectedGroup is < 0 or > 1 || moveX is < short.MinValue or > short.MaxValue || graphicsMoveZ is < short.MinValue or > short.MaxValue)
        {
            throw new NotSupportedException("Native ground query requires the supported normal foot/car entity and exact signed-WORD velocity.");
        }
        int radius = NativeWorldTerrain.ProbeRadius(entityType);
        byte permission = NativeWorldTerrain.PermissionMask(entityType);
        ValidateNativeGroundCache(prior, replacements);
        List<NativeWorldGroundAlternative> alternatives = [];
        WorldGroundSelection? center = null;
        (int X, int Z, int Yaw, bool Blocked)? physical = null;
        foreach (NativeWorldGroundAlternative alternative in prior.Alternatives)
        {
            List<NativeWorldGroundIdentity> cache = [.. alternative.Entries];
            NativeGroundProbe[][][] groups = new NativeGroundProbe[2][][];
            for (int group = 0; group < 2; group++)
            {
                groups[group] = new NativeGroundProbe[8][];
                for (int candidate = 0; candidate < 8; candidate++)
                {
                    (int dx, int dz) = RotateNativeGroundProbe(moveX, graphicsMoveZ, candidate * (group == 0 ? -160 : 160));
                    int x = beforeX + dx;
                    int z = beforeZ - dz;
                    groups[group][candidate] = [new(x, z), new(x - radius, z), new(x, z - radius), new(x + radius, z), new(x, z + radius)];
                }
            }
            Queue<int> cells = new();
            HashSet<int> queued = [];
            Enqueue(groups[0][0][0].Cell);
            byte oldType = ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).GroundType;
            while (cells.TryDequeue(out int cell))
            {
                foreach (int group in new[] { alternative.PreferredGroup, 1 - alternative.PreferredGroup })
                {
                    int? unmatched = null;
                    foreach (NativeGroundProbe[] candidate in groups[group])
                    {
                        foreach (NativeGroundProbe probe in candidate)
                        {
                            if (probe.Status != 2)
                            {
                                continue;
                            }
                            if (probe.Cell != cell)
                            {
                                unmatched ??= probe.Cell;
                                continue;
                            }
                            probe.Selected = ResolveNativeGroundProbe(probe, previousGroundHeight, oldType, cache, replacements);
                            probe.Status = probe.Selected is not null && (probe.Selected.Ground.Triangle.VertexFlags & permission) != 0 ? 0 : 1;
                        }
                        if (candidate.All(probe => probe.Status == 0))
                        {
                            break;
                        }
                    }
                    if (unmatched is int next)
                    {
                        Enqueue(next);
                    }
                }
                // Native53BB90 marks unprocessed probes in a completed block3;
                // they cannot acquire later cache writes from an omitted candidate.
                foreach (NativeGroundProbe probe in groups.SelectMany(group => group).SelectMany(candidate => candidate))
                {
                    if (probe.Status == 2 && probe.Cell == cell)
                    {
                        probe.Status = 3;
                    }
                }
            }
            int[] selections = selectedGroup is int explicitGroup ? selectionMayRetainGroup && explicitGroup != alternative.PreferredGroup ? [explicitGroup, alternative.PreferredGroup] : [explicitGroup] : [alternative.PreferredGroup];
            foreach (int selection in selections)
            {
                int acceptedIndex = Array.FindIndex(groups[selection], candidate => candidate.All(probe => probe.Status == 0));
                List<int> outcomes = [selection];
                if (acceptedIndex < 0)
                {
                    if (alternative.FailedSelections is null)
                    {
                        // Unknown count retains both native branches, rather than zero.
                        outcomes.Add(1 - selection);
                    }
                    else if (alternative.FailedSelections >= 4)
                    {
                        outcomes = [1 - selection];
                    }
                }
                foreach (int chosen in outcomes)
                {
                    acceptedIndex = Array.FindIndex(groups[chosen], candidate => candidate.All(probe => probe.Status == 0));
                    bool blocked = acceptedIndex < 0;
                    NativeGroundProbe? acceptedProbe = blocked ? null : groups[chosen][acceptedIndex][0];
                    WorldGroundSelection? selected = acceptedProbe?.Selected;
                    var outcome = (X: acceptedProbe?.X ?? beforeX, Z: acceptedProbe?.Z ?? beforeZ, Yaw: blocked ? 0 : acceptedIndex * (chosen == 0 ? -160 : 160), Blocked: blocked);
                    if (physical is not null && physical.Value != outcome || center is not null && selected is not null && (center.Ground.Height != selected.Ground.Height || center.Ground.Triangle.GroundType != selected.Ground.Triangle.GroundType || center.Ground.Triangle.TextureFlags != selected.Ground.Triangle.TextureFlags))
                    {
                        throw new NotSupportedException("Retained native world cache/input alternatives disagree on the physical ground result.");
                    }
                    physical ??= outcome;
                    center ??= selected;
                    NativeWorldGroundAlternative nextAlternative = new(chosen, cache.ToArray(), selected?.Identity ?? alternative.CurrentGround, blocked ? alternative.PreviousGround : alternative.CurrentGround) { FailedSelections = blocked ? alternative.FailedSelections is int failures ? unchecked(failures + 1) : null : 0 };
                    if (!alternatives.Any(existing => existing.PreferredGroup == nextAlternative.PreferredGroup && existing.FailedSelections == nextAlternative.FailedSelections && existing.CurrentGround == nextAlternative.CurrentGround && existing.PreviousGround == nextAlternative.PreviousGround && existing.Entries.SequenceEqual(nextAlternative.Entries)))
                    {
                        alternatives.Add(nextAlternative);
                    }
                }
            }
            if (alternatives.Count > 64)
            {
                throw new NotSupportedException("Native retained world ground alternatives exceed the bounded current query representation.");
            }

            void Enqueue(int cell)
            {
                if (queued.Add(cell))
                {
                    cells.Enqueue(cell);
                }
            }
        }
        NativeWorldGroundCacheState result = prior with { Alternatives = alternatives.ToArray() };
        result.Validate();
        return new WorldNativeGroundResult(center, result, physical!.Value.X, physical.Value.Z, physical.Value.Yaw, physical.Value.Blocked);
    }

    private WorldGroundSelection? ResolveNativeGroundProbe(NativeGroundProbe probe, int previousHeight, byte oldType, List<NativeWorldGroundIdentity> cache, IReadOnlyDictionary<int, int> replacements)
    {
        foreach (NativeWorldGroundIdentity identity in cache)
        {
            if (identity.Cell == probe.Cell && TrySelected(identity, probe.X, probe.Z, replacements, out WorldGroundSelection? selected))
            {
                // A cache hit ends the search, even if its permission subsequently refuses.
                return selected;
            }
        }
        WorldGroundSelection? nearest = null;
        int distance = int.MaxValue;
        foreach (WorldGroundSelection candidate in ContainingNativeGround(probe.Cell, probe.X, probe.Z, replacements))
        {
            int delta = Math.Abs((int)candidate.Ground.Height - previousHeight);
            if ((oldType <= 5 || candidate.Ground.Triangle.GroundType <= 5 || delta < 200) && delta < distance)
            {
                nearest = candidate;
                distance = delta;
            }
        }
        if (nearest is not null)
        {
            // Native insertion precedes permission validation and does not deduplicate pointers.
            cache.Insert(0, nearest.Identity);
            if (cache.Count > 12)
            {
                cache.RemoveAt(12);
            }
        }
        return nearest;
    }

    private IEnumerable<WorldGroundSelection> ContainingNativeGround(int cell, int x, int z, IReadOnlyDictionary<int, int> replacements)
    {
        int map = cell / 128 / 4 * 32 + cell % 128 / 4;
        int block = cell / 128 % 4 * 4 + cell % 128 % 4;
        WorldSegment segment = GetSegment(map, replacements);
        foreach (WorldTriangle triangle in segment.Triangles.Where(triangle => triangle.NativeBlockIndex == block))
        {
            if (WorldAssetReader.TryNativeHeight(triangle, x, z, out int height))
            {
                yield return new WorldGroundSelection(new NativeWorldGroundIdentity(map, segment.SourceId, block, triangle.NativePolygonIndex), new WorldGround(height, triangle, map));
            }
        }
    }

    private bool TrySelected(NativeWorldGroundIdentity identity, int x, int z, IReadOnlyDictionary<int, int> replacements, out WorldGroundSelection? selected)
    {
        WorldTriangle triangle = ResolveNativeGroundIdentity(identity, replacements);
        if (WorldAssetReader.TryNativeHeight(triangle, x, z, out int height))
        {
            selected = new WorldGroundSelection(identity, new WorldGround(height, triangle, identity.MapId));
            return true;
        }
        selected = null;
        return false;
    }

    private static int NativeCell(int x, int rawZ) => (rawZ + 294912) % 196608 / 2048 * 128 + (x + 393216) % 262144 / 2048;

    private static (int X, int Z) RotateNativeGroundProbe(int x, int z, int yaw)
    {
        int cosine = (int)Math.Round(Math.Cos(yaw * 0.0015339808305725455) * 4096);
        int sine = (int)Math.Round(Math.Sin(yaw * 0.0015339808305725455) * 4096);
        return ((int)Math.Round((cosine * x + sine * z) / 4096d), (int)Math.Round((-sine * x + cosine * z) / 4096d));
    }
}
