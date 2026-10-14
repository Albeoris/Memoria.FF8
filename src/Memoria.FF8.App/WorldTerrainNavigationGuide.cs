using Memoria.FF8.Assets;

namespace Memoria.FF8;

internal sealed record WorldTerrainWaypoint(int X, int RawZ, int GroundHeight);
internal sealed record WorldTerrainRoute(IReadOnlyList<WorldTerrainWaypoint> Waypoints, int ExpandedNodes, int ProbeCount, string? Refusal);

/// <summary>Read-only terrain advice; ordinary movement must still execute and validate every actual step.</summary>
internal static class WorldTerrainNavigationGuide
{
    public static WorldTerrainRoute Plan(WorldAssets assets, int startX, int startRawZ, int previousGroundHeight, ushort replacementMask, ushort storyWord, int targetX, int targetRawZ, int grid = 128, int padding = 4096, int maximumNodes = 25000, bool simplify = true, int advisoryClearance = 0)
    {
        if (grid is not (64 or 128) || padding is < 0 or > 8192 || maximumNodes is < 1 or > 100000 || advisoryClearance is < 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(grid));
        }
        if (startX is < -131072 or >= 131072 || targetX is < -131072 or >= 131072 || startRawZ is < -98304 or >= 98304 || targetRawZ is < -98304 or >= 98304)
        {
            throw new ArgumentOutOfRangeException(nameof(startX), "Terrain advice requires centered native coordinates and does not plan across world wrapping seams.");
        }
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(replacementMask);
        WorldGround? targetGround = assets.FindNativeGround(targetX, targetRawZ, replacements);
        WorldFieldEntry? targetEntry = targetGround is WorldGround target ? assets.FieldEntries.Select(new WorldFieldSelectionInput(targetX, targetRawZ, storyWord, 0), target.Triangle.TextureFlags)?.Entry : null;
        int probes = 1;
        int minimumX = Math.Max(-131072, Math.Min(startX, targetX) - padding);
        int maximumX = Math.Min(131071, Math.Max(startX, targetX) + padding);
        int minimumZ = Math.Max(-98304, Math.Min(startRawZ, targetRawZ) - padding);
        int maximumZ = Math.Min(98303, Math.Max(startRawZ, targetRawZ) + padding);
        WorldTerrainWaypoint start = new(startX, startRawZ, previousGroundHeight);
        PriorityQueue<WorldTerrainWaypoint, double> open = new();
        Dictionary<WorldTerrainWaypoint, double> costs = new() { [start] = 0 };
        Dictionary<WorldTerrainWaypoint, WorldTerrainWaypoint> parents = [];
        HashSet<WorldTerrainWaypoint> closed = [];
        open.Enqueue(start, Distance(start.X, start.RawZ, targetX, targetRawZ));
        int expanded = 0;
        while (open.TryDequeue(out WorldTerrainWaypoint? current, out _) && expanded < maximumNodes)
        {
            if (!closed.Add(current))
            {
                continue;
            }
            expanded++;
            if (Distance(current.X, current.RawZ, targetX, targetRawZ) <= 1024 && Traverse(current, targetX, targetRawZ, true) is WorldTerrainWaypoint destination)
            {
                List<WorldTerrainWaypoint> path = [destination, current];
                while (parents.TryGetValue(current, out WorldTerrainWaypoint? parent))
                {
                    path.Add(parent);
                    current = parent;
                }
                path.Reverse();
                if (!simplify)
                {
                    WorldTerrainWaypoint finalStart = path[^2];
                    WorldTerrainWaypoint finalDestination = path[^1];
                    path.RemoveAt(path.Count - 1);
                    int finalSamples = Math.Max(1, (int)Math.Ceiling(Distance(finalStart.X, finalStart.RawZ, finalDestination.X, finalDestination.RawZ) / 48));
                    WorldTerrainWaypoint previous = finalStart;
                    for (int sample = 1; sample <= finalSamples; sample++)
                    {
                        int x = finalStart.X + (int)Math.Round((finalDestination.X - (double)finalStart.X) * sample / finalSamples);
                        int z = finalStart.RawZ + (int)Math.Round((finalDestination.RawZ - (double)finalStart.RawZ) * sample / finalSamples);
                        previous = Traverse(previous, x, z, true) ?? throw new InvalidOperationException("Certified terminal advisory segment changed during subdivision.");
                        path.Add(previous);
                    }
                    return new WorldTerrainRoute(path, expanded, probes, null);
                }
                List<WorldTerrainWaypoint> simplified = [path[0]];
                int index = 0;
                while (index < path.Count - 1)
                {
                    int candidate = path.Count - 1;
                    while (candidate > index + 1 && (Traverse(path[index], path[candidate].X, path[candidate].RawZ, candidate == path.Count - 1) is not WorldTerrainWaypoint reached || reached.GroundHeight != path[candidate].GroundHeight))
                    {
                        candidate--;
                    }
                    simplified.Add(path[candidate]);
                    index = candidate;
                }
                return new WorldTerrainRoute(simplified, expanded, probes, null);
            }
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }
                    int x = current.X + dx * grid;
                    int z = current.RawZ + dz * grid;
                    if (x < minimumX || x > maximumX || z < minimumZ || z > maximumZ || Traverse(current, x, z, false) is not WorldTerrainWaypoint next || closed.Contains(next))
                    {
                        continue;
                    }
                    double cost = costs[current] + grid * Math.Sqrt(dx * dx + dz * dz);
                    if (costs.TryGetValue(next, out double known) && known <= cost)
                    {
                        continue;
                    }
                    costs[next] = cost;
                    parents[next] = current;
                    open.Enqueue(next, cost + Distance(x, z, targetX, targetRawZ));
                }
            }
        }
        return new WorldTerrainRoute([], expanded, probes, "No certified advisory path within the bounded search; original movement and scene remain untouched.");

        WorldTerrainWaypoint? Traverse(WorldTerrainWaypoint from, int x, int z, bool terminal)
        {
            int samples = Math.Max(1, (int)Math.Ceiling(Distance(from.X, from.RawZ, x, z) / 16));
            int height = from.GroundHeight;
            probes++;
            byte? previousType = assets.FindNativeGround(from.X, from.RawZ, replacements, height)?.Triangle.GroundType;
            for (int sample = 1; sample <= samples; sample++)
            {
                int sampleX = from.X + (int)Math.Round((x - (double)from.X) * sample / samples);
                int sampleZ = from.RawZ + (int)Math.Round((z - (double)from.RawZ) * sample / samples);
                if (Probe(sampleX, sampleZ, height, previousType) is not WorldGround center)
                {
                    return null;
                }
                foreach ((int fanX, int fanZ) in new (int, int)[] { (-32, 0), (0, 32), (32, 0), (0, -32) })
                {
                    if (Probe(sampleX + fanX, sampleZ + fanZ, height, previousType) is not WorldGround fan || !AllowsEntrance(fan, sampleX + fanX, sampleZ + fanZ, terminal))
                    {
                        return null;
                    }
                }
                if (advisoryClearance != 0)
                {
                    int radius = 32 + advisoryClearance;
                    foreach ((int fanX, int fanZ) in new (int, int)[] { (-radius, 0), (0, radius), (radius, 0), (0, -radius) })
                    {
                        if (Probe(sampleX + fanX, sampleZ + fanZ, height, previousType) is not WorldGround fan || !AllowsEntrance(fan, sampleX + fanX, sampleZ + fanZ, terminal))
                        {
                            return null;
                        }
                    }
                }
                if (!AllowsEntrance(center, sampleX, sampleZ, terminal))
                {
                    return null;
                }
                height = (int)center.Height;
                previousType = center.Triangle.GroundType;
            }
            return new WorldTerrainWaypoint(x, z, height);
        }

        WorldGround? Probe(int x, int z, int height, byte? previousType)
        {
            probes++;
            return assets.FindNativeGround(x, z, replacements, height, previousGroundType: previousType);
        }

        bool AllowsEntrance(WorldGround ground, int x, int z, bool terminal)
        {
            return !ground.Triangle.FieldEntrance || terminal && targetEntry is not null && assets.FieldEntries.Select(new WorldFieldSelectionInput(x, z, storyWord, 0), ground.Triangle.TextureFlags)?.Entry == targetEntry;
        }
    }

    private static double Distance(int x, int z, int targetX, int targetZ)
    {
        double dx = targetX - (double)x;
        double dz = targetZ - (double)z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
