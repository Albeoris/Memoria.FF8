using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private IReadOnlyList<WorldAutoPlayTarget>? autoPlayEntrances;
    private IReadOnlyDictionary<WorldAutoPlayTarget, IReadOnlyList<(int X, int RawZ)>>? autoPlayEntrancePaths;
    private GameState? autoPlayEntranceState;
    private AutoPlayTerrain? autoPlayTerrain;

    private sealed class AutoPlayTerrain(int entityType)
    {
        public int EntityType { get; } = entityType;
        public List<(WorldTriangle Triangle, NativeWorldGroundIdentity Identity)> Polygons { get; } = [];
        public Dictionary<NativeWorldGroundIdentity, int> Indices { get; } = [];
        public Dictionary<(Vector3 A, Vector3 B), List<int>> Edges { get; } = [];
        public HashSet<int> LoadedSegments { get; } = [];
        public HashSet<int> ExpandedSegments { get; } = [];
    }

    /// <summary>Native active entrances in the current owner's connected terrain component become automatic frontiers.</summary>
    public IReadOnlyList<WorldAutoPlayTarget> DiscoverAutoPlayEntrances()
    {
        return DiscoverAutoPlayEntrances(null, null);
    }

    private IReadOnlyList<WorldAutoPlayTarget> DiscoverAutoPlayEntrances(IReadOnlySet<(int X, int RawZ)>? avoidedCrossings, string? targetId)
    {
        int entityType = RequireAutoPlayOwner();
        WorldState before = State;
        ushort story = WorldEntryVariables.Read(session.State).StoryWord;
        if (avoidedCrossings is null && autoPlayEntrances is not null && ReferenceEquals(autoPlayEntranceState, session.State))
        {
            return autoPlayEntrances;
        }
        if (autoPlayTerrain is null || autoPlayTerrain.EntityType != entityType)
        {
            autoPlayTerrain = new(entityType);
        }
        AutoPlayTerrain topology = autoPlayTerrain;
        var terrain = topology.Polygons;
        var edges = topology.Edges;
        WorldGroundSelection start = assets.LookupNativeGround(before.X, before.Z, replacements, []) ?? throw new InvalidOperationException("World discovery requires the actual native starting ground.");
        LoadSegment(start.Identity.MapId);
        if (!topology.Indices.TryGetValue(start.Identity, out int startIndex))
        {
            throw new NotSupportedException("The current world polygon is outside its owner's traversable discovery component.");
        }
        HashSet<int> component = [];
        Dictionary<int, (int Parent, (int X, int RawZ) Crossing)> parents = [];
        Queue<int> pending = new();
        pending.Enqueue(startIndex);
        while (pending.TryDequeue(out int index))
        {
            if (!component.Add(index))
            {
                continue;
            }
            int mapId = terrain[index].Identity.MapId;
            if (topology.ExpandedSegments.Add(mapId))
            {
                int row = mapId / WorldAssets.Columns;
                int column = mapId % WorldAssets.Columns;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int adjacentRow = (row + dz + WorldAssets.Rows) % WorldAssets.Rows;
                        int adjacentColumn = (column + dx + WorldAssets.Columns) % WorldAssets.Columns;
                        LoadSegment(adjacentRow * WorldAssets.Columns + adjacentColumn);
                    }
                }
            }
            WorldTriangle triangle = terrain[index].Triangle;
            foreach ((Vector3 a, Vector3 b) in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
            {
                var crossing = Crossing(a, b);
                if (avoidedCrossings?.Contains(crossing) == true)
                {
                    continue;
                }
                foreach (int adjacent in edges[Edge(a, b)])
                {
                    if (!component.Contains(adjacent) && parents.TryAdd(adjacent, (index, crossing)))
                    {
                        pending.Enqueue(adjacent);
                    }
                }
            }
        }
        List<(WorldAutoPlayTarget Target, int Triangle)> reachable = [];
        foreach (int index in component.Order())
        {
            WorldTriangle triangle = terrain[index].Triangle;
            if (!triangle.FieldEntrance)
            {
                continue;
            }
            int x = (int)MathF.Round((triangle.A.X + triangle.B.X + triangle.C.X) / 3);
            int z = (int)MathF.Round((triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3);
            if (avoidedCrossings?.Contains((x, z)) == true)
            {
                continue;
            }
            WorldGroundSelection? native = assets.LookupNativeGround(x, z, replacements, []);
            if (native is null || native.Identity != terrain[index].Identity)
            {
                continue;
            }
            WorldState candidate;
            if (before.NativeCarRuntime is not null)
            {
                if (!TryPrepareCurrentCarPlacement(x, z, out WorldState? carCandidate))
                {
                    continue;
                }
                candidate = carCandidate;
            }
            else
            {
                candidate = PrepareAutoPlayFootPlacement(before, native, x, z);
            }
            WorldFieldSelection? selection = SelectAutoPlayEntrance(candidate, triangle.TextureFlags, story, entityType);
            if (selection is null)
            {
                continue;
            }
            ushort field = selection.Entry.FieldId;
            string id = $"world-entry:{field}:{selection.Entry.Index}";
            if (targetId is not null && id != targetId)
            {
                continue;
            }
            int order = field switch
            {
                774 when IsAutoPlayMissileBaseCar(before, story) => -1,
                106 when story == 750 && before.NativeFootRuntime?.GardenDismount is not null => -1,
                159 => 0,
                106 => 10,
                136 => 20,
                _ => 100
            };
            reachable.Add((new WorldAutoPlayTarget(id, x, z, field, order), index));
        }
        var ordered = reachable.GroupBy(item => item.Target.DestinationField)
            .Select(group => group.OrderBy(item => SquaredDistance(before.X, before.Z, item.Target.X, item.Target.RawZ)).ThenBy(item => item.Target.Id, StringComparer.Ordinal).ThenBy(item => item.Target.X).ThenBy(item => item.Target.RawZ).First())
            .OrderBy(item => item.Target.Order).ThenBy(item => SquaredDistance(before.X, before.Z, item.Target.X, item.Target.RawZ)).ThenBy(item => item.Target.Id, StringComparer.Ordinal);
        Dictionary<WorldAutoPlayTarget, IReadOnlyList<(int X, int RawZ)>> paths = [];
        int ordinaryRank = 100;
        foreach (var item in ordered)
        {
            WorldAutoPlayTarget target = item.Target.Order == 100 ? item.Target with { Order = ordinaryRank++ } : item.Target;
            List<(int X, int RawZ)> path = [(target.X, target.RawZ)];
            for (int index = item.Triangle; index != startIndex;)
            {
                var parent = parents[index];
                if (path[^1] != parent.Crossing)
                {
                    path.Add(parent.Crossing);
                }
                index = parent.Parent;
            }
            path.Reverse();
            paths.Add(target, path.ToArray());
        }
        autoPlayEntranceState = avoidedCrossings is null ? session.State : null;
        autoPlayEntrancePaths = paths;
        autoPlayEntrances = paths.Keys.ToArray();
        return autoPlayEntrances;

        void LoadSegment(int mapId)
        {
            if (!topology.LoadedSegments.Add(mapId))
            {
                return;
            }
            WorldSegment segment = assets.GetSegment(mapId, replacements);
            foreach (WorldTriangle triangle in segment.Triangles.Where(item => AutoPlayCanTraverse(item, entityType)))
            {
                int index = terrain.Count;
                NativeWorldGroundIdentity identity = new(segment.MapId, segment.SourceId, triangle.NativeBlockIndex, triangle.NativePolygonIndex);
                terrain.Add((triangle, identity));
                topology.Indices.TryAdd(identity, index);
                foreach ((Vector3 a, Vector3 b) in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
                {
                    var edge = Edge(a, b);
                    if (!edges.TryGetValue(edge, out List<int>? neighbors))
                    {
                        neighbors = [];
                        edges.Add(edge, neighbors);
                    }
                    neighbors.Add(index);
                }
            }
        }
    }

    public IReadOnlyList<(int X, int RawZ)> GetAutoPlayEntrancePath(WorldAutoPlayTarget target)
    {
        DiscoverAutoPlayEntrances();
        return autoPlayEntrancePaths!.TryGetValue(target, out var path) ? path : throw new InvalidOperationException("The requested entrance has no path from the current world state.");
    }

    internal IReadOnlyList<(int X, int RawZ)>? ReplanAutoPlayEntrance(WorldAutoPlayTarget target, IReadOnlySet<(int X, int RawZ)> avoidedCrossings)
    {
        WorldAutoPlayTarget? replacement = DiscoverAutoPlayEntrances(avoidedCrossings, target.Id).SingleOrDefault();
        return replacement is null ? null : autoPlayEntrancePaths![replacement];
    }

    public bool IsAtAutoPlayEntrance(WorldAutoPlayTarget target)
    {
        WorldState current = State;
        int? entityType = current.NativeFootRuntime?.EntityType ?? current.NativeCarRuntime?.EntityType;
        NativeWorldGroundCacheState? ground = current.NativeFootRuntime?.GroundCache ?? current.NativeCarRuntime?.GroundCache;
        if (entityType is not int owner || ground is null || ground.Alternatives.Length == 0)
        {
            return false;
        }
        ushort story = WorldEntryVariables.Read(session.State).StoryWord;
        foreach (NativeWorldGroundAlternative alternative in ground.Alternatives)
        {
            byte flags = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).TextureFlags;
            WorldFieldSelection? selection = SelectAutoPlayEntrance(current, flags, story, owner);
            if (selection is null || selection.Entry.FieldId != target.DestinationField || target.Id != $"world-entry:{selection.Entry.FieldId}:{selection.Entry.Index}")
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Relocates the current debug-controlled owner; ordinary callbacks retain field-entry and saved-return ownership.</summary>
    public void DebugTeleportToEntrance(WorldAutoPlayTarget target)
    {
        int entityType = RequireAutoPlayOwner(debugPlacement: true);
        GameState departure = session.State;
        WorldState before = State;
        WorldGroundSelection ground = assets.LookupNativeGround(target.X, target.RawZ, replacements, []) ?? throw new InvalidOperationException("World teleport requires an actual native ground polygon.");
        ushort story = WorldEntryVariables.Read(session.State).StoryWord;
        if (!AutoPlayCanTraverse(ground.Ground.Triangle, entityType))
        {
            throw new InvalidOperationException("World teleport destination is not traversable by the current native owner.");
        }
        WorldState relocated = before.NativeCarRuntime is not null ? PrepareCurrentCarPlacement(target.X, target.RawZ) : PrepareAutoPlayFootPlacement(before, ground, target.X, target.RawZ);
        WorldFieldSelection? selection = SelectAutoPlayEntrance(relocated, ground.Ground.Triangle.TextureFlags, story, entityType);
        if (selection?.Entry.FieldId != target.DestinationField)
        {
            throw new InvalidOperationException("World teleport destination no longer resolves to the requested native entrance.");
        }
        if (relocated.NativeFootRuntime is not null)
        {
            EnsureContactCertificate(relocated);
        }
        if (!ReferenceEquals(session.State, departure))
        {
            throw new InvalidOperationException("The current world state changed before diagnostic entrance placement publication.");
        }
        session.SetWorldState(relocated);
    }

    private static WorldState PrepareAutoPlayFootPlacement(WorldState before, WorldGroundSelection ground, int x, int rawZ)
    {
        NativeFootWorldRuntimeState foot = before.NativeFootRuntime!;
        int height = NativeWorldTerrain.ResolveHeight(foot.EntityType, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, before.NativeHeight).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity, foot.GroundCache);
        return before with
        {
            X = x,
            Z = rawZ,
            NativeHeight = height,
            CameraVelocity = 0,
            CameraTargetX = x,
            CameraTargetZ = rawZ,
            CameraTargetNativeHeight = height - 256,
            GroundType = ground.Ground.Triangle.GroundType,
            NativeFootRuntime = foot with { GroundCache = cache, PreviousHorizontalInput = 0 }
        };
    }

    private WorldFieldSelection? SelectAutoPlayEntrance(WorldState candidate, byte textureFlags, ushort story, int entityType)
    {
        NativeSavedWorldState saved = session.State.SavedWorld ?? throw new NotSupportedException("World entrance discovery requires saved-world authority.");
        return assets.FieldEntries.Select(new WorldFieldSelectionInput(candidate.X, candidate.Z, story, entityType), textureFlags, (opcode, operand) => EvaluateWorldEventPredicate(candidate, 0, saved, opcode, operand, eventUi: candidate.EventUi));
    }

    private int RequireAutoPlayOwner(bool debugPlacement = false)
    {
        WorldState world = State;
        if (world.NativeCarRuntime is { } car && IsAutoPlayCarStationary(car) && (car.EntityType == 33 || !debugPlacement && IsAutoPlayMissileBaseCar(world, WorldEntryVariables.Read(session.State).StoryWord)) && world.NativeFootRuntime is null && world.NativeGardenRuntime is null && world.NativeRagnarokRuntime is null && world.NativeTrainRuntime is null && world.NativeScriptedRuntime is null)
        {
            car.Validate();
            return car.EntityType;
        }
        bool parkedGarden = !debugPlacement && world.NativeGardenRuntime is { Transfer.Counter: 31 } && world.NativeFootRuntime?.GardenDismount is not null;
        if (world.NativeFootRuntime is not { } foot || !NativeWorldEntityTypes.IsSupportedFoot(foot.EntityType) || debugPlacement && (foot.EntityType is not (0 or 1) || foot.NormalObjects is not null || foot.StationTrains is not null) || world.NativeCarRuntime is not null || world.NativeGardenRuntime is not null && !parkedGarden || world.NativeRagnarokRuntime is not null || world.NativeTrainRuntime is not null || world.NativeScriptedRuntime is not null)
        {
            throw new NotSupportedException(debugPlacement ? "Automatic coordinate travel requires ordinary foot without auxiliary objects, or the current stationary type33 car." : "Automatic world discovery requires a supported native foot owner or an admitted stationary car.");
        }
        foot.RequireNativeGroundState();
        return foot.EntityType;
    }

    internal static bool IsAutoPlayCarStationary(NativeCarWorldRuntimeState car)
    {
        return car is { Speed: 0, MovementMagnitude20409F4: 0, TerrainTiltBlend: 0 };
    }

    internal static bool IsAutoPlayMissileBaseCar(WorldState world, int story)
    {
        // NG-026: The native missile base approach requires this mission vehicle.
        return story == 455 && world.NativeCarRuntime is { EntityType: 32, ShapeId: 74, FootEntityType: 6 };
    }

    private static bool AutoPlayCanTraverse(WorldTriangle triangle, int entityType)
    {
        uint flags = triangle.GroundType | (uint)triangle.TextureFlags << 8 | (uint)triangle.VertexFlags << 16;
        return NativeWorldTerrain.CanTraverse(flags, entityType);
    }

    private static long SquaredDistance(int x, int z, int targetX, int targetZ)
    {
        long dx = NearWrapped(targetX, x, 262144) - (long)x;
        long dz = NearWrapped(targetZ, z, 196608) - (long)z;
        return dx * dx + dz * dz;
    }

    private static (int X, int RawZ) Crossing(Vector3 a, Vector3 b)
    {
        return ((int)WorldAssets.Wrap(MathF.Round((a.X + b.X) / 2), 262144), (int)WorldAssets.Wrap(MathF.Round((a.Z + b.Z) / 2), 196608));
    }

    private static int CompareVertices(Vector3 a, Vector3 b)
    {
        int comparison = a.X.CompareTo(b.X);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = a.Y.CompareTo(b.Y);
        return comparison != 0 ? comparison : a.Z.CompareTo(b.Z);
    }

    private static (Vector3 A, Vector3 B) Edge(Vector3 a, Vector3 b)
    {
        a = a with { X = WorldAssets.Wrap(a.X, 262144), Z = WorldAssets.Wrap(a.Z, 196608) };
        b = b with { X = WorldAssets.Wrap(b.X, 262144), Z = WorldAssets.Wrap(b.Z, 196608) };
        return CompareVertices(a, b) < 0 ? (a, b) : (b, a);
    }
}
