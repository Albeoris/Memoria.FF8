using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record WorldGroundSelection(NativeWorldGroundIdentity Identity, WorldGround Ground);
public sealed record WorldCarGroundResult(WorldGroundSelection? Center, NativeCarGroundCacheState Cache, int X, int Z, int GroundYaw, bool Blocked);

public sealed partial class WorldAssets
{
    private readonly Dictionary<NativeWorldGroundIdentity, WorldTriangle> carGroundPolygons = [];
    private sealed class CarProbe(int x, int z)
    {
        public int X { get; } = (int)Wrap(x, 262144);
        public int Z { get; } = (int)Wrap(z, 196608);
        public int Cell => NativeCell(X, Z);
        public int Status { get; set; } = 2;
        public WorldGroundSelection? Selected { get; set; }
    }

    public WorldGroundSelection? LookupCarGround(int x, int z, IReadOnlyDictionary<int, int> replacements, IReadOnlyList<NativeWorldGroundIdentity> cache)
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
        foreach (WorldGroundSelection selected in ContainingCarGround(cell, x, z, replacements))
        {
            // Native53EB80 is a lookup, not a cache insertion or permission/nearest-height search.
            return selected;
        }
        return null;
    }

    public WorldTriangle ResolveCarGroundIdentity(NativeWorldGroundIdentity identity, IReadOnlyDictionary<int, int> replacements)
    {
        identity.Validate();
        int expectedSource = replacements.TryGetValue(identity.MapId, out int replacement) ? replacement : identity.MapId;
        if (identity.SourceId != expectedSource)
        {
            throw new InvalidDataException("Native cached ground source differs from the current original replacement selection.");
        }
        if (carGroundPolygons.TryGetValue(identity, out WorldTriangle? cached))
        {
            return cached;
        }
        WorldSegment segment = GetSegment(identity.MapId, replacements);
        if (segment.SourceId != identity.SourceId)
        {
            throw new InvalidDataException("Native cached ground source differs from the current original replacement selection.");
        }
        WorldTriangle triangle = segment.Triangles.SingleOrDefault(triangle => triangle.NativeBlockIndex == identity.BlockIndex && triangle.NativePolygonIndex == identity.PolygonIndex) ?? throw new InvalidDataException("Native cached original polygon is absent.");
        carGroundPolygons.Add(identity, triangle);
        return triangle;
    }

    public void ValidateCarGroundCache(NativeCarGroundCacheState state, IReadOnlyDictionary<int, int> replacements)
    {
        state.Validate();
        foreach (NativeCarGroundAlternative alternative in state.Alternatives)
        {
            _ = ResolveCarGroundIdentity(alternative.CurrentGround, replacements);
            _ = ResolveCarGroundIdentity(alternative.PreviousGround, replacements);
            foreach (NativeWorldGroundIdentity entry in alternative.Entries)
            {
                _ = ResolveCarGroundIdentity(entry, replacements);
            }
        }
    }

    public WorldCarGroundResult ResolveCarGround(int beforeX, int beforeZ, int moveX, int graphicsMoveZ, int previousGroundHeight, int? selectedGroup, NativeCarGroundCacheState prior, IReadOnlyDictionary<int, int> replacements)
    {
        ValidateCarGroundCache(prior, replacements);
        List<NativeCarGroundAlternative> alternatives = [];
        WorldGroundSelection? center = null;
        (int X, int Z, int Yaw, bool Blocked)? physical = null;
        foreach (NativeCarGroundAlternative alternative in prior.Alternatives)
        {
            List<NativeWorldGroundIdentity> cache = [.. alternative.Entries];
            CarProbe[][][] groups = new CarProbe[2][][];
            for (int group = 0; group < 2; group++)
            {
                groups[group] = new CarProbe[8][];
                for (int candidate = 0; candidate < 8; candidate++)
                {
                    (int dx, int dz) = RotateCarProbe(moveX, graphicsMoveZ, candidate * (group == 0 ? -160 : 160));
                    int x = beforeX + dx;
                    int z = beforeZ - dz;
                    groups[group][candidate] = [new(x, z), new(x - 64, z), new(x, z - 64), new(x + 64, z), new(x, z + 64)];
                }
            }
            Queue<int> cells = new();
            HashSet<int> queued = [];
            Enqueue(groups[0][0][0].Cell);
            byte oldType = ResolveCarGroundIdentity(alternative.CurrentGround, replacements).GroundType;
            while (cells.TryDequeue(out int cell))
            {
                foreach (int group in new[] { alternative.PreferredGroup, 1 - alternative.PreferredGroup })
                {
                    int? unmatched = null;
                    foreach (CarProbe[] candidate in groups[group])
                    {
                        foreach (CarProbe probe in candidate)
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
                            probe.Selected = ResolveCarProbe(probe, previousGroundHeight, oldType, cache, replacements);
                            probe.Status = probe.Selected is not null && (probe.Selected.Ground.Triangle.VertexFlags & 0x40) != 0 ? 0 : 1;
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
                foreach (CarProbe probe in groups.SelectMany(group => group).SelectMany(candidate => candidate))
                {
                    if (probe.Status == 2 && probe.Cell == cell)
                    {
                        probe.Status = 3;
                    }
                }
            }
            int chosen = selectedGroup ?? alternative.PreferredGroup;
            int acceptedIndex = Array.FindIndex(groups[chosen], candidate => candidate.All(probe => probe.Status == 0));
            if (acceptedIndex < 0)
            {
                if (alternative.FailedSelections is null)
                {
                    throw new NotSupportedException("Native car opposite-group selection requires an executed retained failure count.");
                }
                if (alternative.FailedSelections >= 4)
                {
                    chosen = 1 - chosen;
                    acceptedIndex = Array.FindIndex(groups[chosen], candidate => candidate.All(probe => probe.Status == 0));
                }
            }
            bool blocked = acceptedIndex < 0;
            CarProbe? acceptedProbe = blocked ? null : groups[chosen][acceptedIndex][0];
            WorldGroundSelection? selected = acceptedProbe?.Selected;
            var outcome = (X: acceptedProbe?.X ?? beforeX, Z: acceptedProbe?.Z ?? beforeZ, Yaw: blocked ? 0 : acceptedIndex * (chosen == 0 ? -160 : 160), Blocked: blocked);
            if (physical is not null && physical.Value != outcome || center is not null && selected is not null && (center.Ground.Height != selected.Ground.Height || center.Ground.Triangle.GroundType != selected.Ground.Triangle.GroundType || center.Ground.Triangle.TextureFlags != selected.Ground.Triangle.TextureFlags))
            {
                throw new NotSupportedException("Retained native car cache alternatives disagree on the selected physical ground result.");
            }
            physical ??= outcome;
            center ??= selected;
            NativeCarGroundAlternative nextAlternative = new(chosen, cache.ToArray(), selected?.Identity ?? alternative.CurrentGround, blocked ? alternative.PreviousGround : alternative.CurrentGround) { FailedSelections = blocked ? checked(alternative.FailedSelections!.Value + 1) : 0 };
            if (!alternatives.Any(existing => existing.PreferredGroup == nextAlternative.PreferredGroup && existing.FailedSelections == nextAlternative.FailedSelections && existing.CurrentGround == nextAlternative.CurrentGround && existing.PreviousGround == nextAlternative.PreviousGround && existing.Entries.SequenceEqual(nextAlternative.Entries)))
            {
                alternatives.Add(nextAlternative);
            }

            void Enqueue(int cell)
            {
                if (queued.Add(cell))
                {
                    cells.Enqueue(cell);
                }
            }
        }
        NativeCarGroundCacheState result = prior with { Alternatives = alternatives.ToArray() };
        result.Validate();
        return new WorldCarGroundResult(center, result, physical!.Value.X, physical.Value.Z, physical.Value.Yaw, physical.Value.Blocked);
    }

    private WorldGroundSelection? ResolveCarProbe(CarProbe probe, int previousHeight, byte oldType, List<NativeWorldGroundIdentity> cache, IReadOnlyDictionary<int, int> replacements)
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
        foreach (WorldGroundSelection candidate in ContainingCarGround(probe.Cell, probe.X, probe.Z, replacements))
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

    private IEnumerable<WorldGroundSelection> ContainingCarGround(int cell, int x, int z, IReadOnlyDictionary<int, int> replacements)
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
        WorldTriangle triangle = ResolveCarGroundIdentity(identity, replacements);
        if (WorldAssetReader.TryNativeHeight(triangle, x, z, out int height))
        {
            selected = new WorldGroundSelection(identity, new WorldGround(height, triangle, identity.MapId));
            return true;
        }
        selected = null;
        return false;
    }

    private static int NativeCell(int x, int rawZ) => (rawZ + 294912) % 196608 / 2048 * 128 + (x + 393216) % 262144 / 2048;

    private static (int X, int Z) RotateCarProbe(int x, int z, int yaw)
    {
        int cosine = (int)Math.Round(Math.Cos(yaw * 0.0015339808305725455) * 4096);
        int sine = (int)Math.Round(Math.Sin(yaw * 0.0015339808305725455) * 4096);
        return ((int)Math.Round((cosine * x + sine * z) / 4096d), (int)Math.Round((-sine * x + cosine * z) / 4096d));
    }
}
