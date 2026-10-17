using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private IReadOnlyList<WorldAutoPlayTarget>? autoPlayEntrances;
    private GameState? autoPlayEntranceState;

    /// <summary>Only native active entrances in the current connected walkable terrain component become automatic frontiers.</summary>
    public IReadOnlyList<WorldAutoPlayTarget> DiscoverAutoPlayEntrances()
    {
        RequireAutoPlayFoot();
        WorldState before = State;
        ushort story = WorldEntryVariables.Read(session.State).StoryWord;
        if (autoPlayEntrances is not null && ReferenceEquals(autoPlayEntranceState, session.State))
        {
            return autoPlayEntrances;
        }
        List<(WorldTriangle Triangle, NativeWorldGroundIdentity Identity)> terrain = [];
        Dictionary<(Vector3 A, Vector3 B), List<int>> edges = [];
        HashSet<int> loadedSegments = [];
        HashSet<int> expandedSegments = [];
        WorldGroundSelection start = assets.LookupNativeGround(before.X, before.Z, replacements, []) ?? throw new InvalidOperationException("World discovery requires the actual native starting ground.");
        LoadSegment(start.Identity.MapId);
        int startIndex = terrain.FindIndex(item => item.Identity == start.Identity);
        if (startIndex < 0)
        {
            throw new NotSupportedException("The current world polygon is outside the walkable discovery component.");
        }
        HashSet<int> component = [];
        Queue<int> pending = new();
        pending.Enqueue(startIndex);
        while (pending.TryDequeue(out int index))
        {
            if (!component.Add(index))
            {
                continue;
            }
            int mapId = terrain[index].Identity.MapId;
            if (expandedSegments.Add(mapId))
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
                foreach (int adjacent in edges[Edge(a, b)])
                {
                    if (!component.Contains(adjacent))
                    {
                        pending.Enqueue(adjacent);
                    }
                }
            }
        }
        List<WorldAutoPlayTarget> reachable = [];
        foreach (int index in component.Order())
        {
            WorldTriangle triangle = terrain[index].Triangle;
            if (!triangle.FieldEntrance)
            {
                continue;
            }
            int x = (int)MathF.Round((triangle.A.X + triangle.B.X + triangle.C.X) / 3);
            int z = (int)MathF.Round((triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3);
            WorldGroundSelection? native = assets.LookupNativeGround(x, z, replacements, []);
            if (native?.Identity != terrain[index].Identity)
            {
                continue;
            }
            WorldFieldSelection? selection = SelectAutoPlayEntrance(x, z, triangle.TextureFlags, story);
            if (selection is null)
            {
                continue;
            }
            ushort field = selection.Entry.FieldId;
            int order = field switch
            {
                159 => 0,
                106 => 10,
                136 => 20,
                _ => 100
            };
            reachable.Add(new WorldAutoPlayTarget($"world-entry:{field}:{selection.Entry.Index}", x, z, field, order));
        }
        autoPlayEntranceState = session.State;
        autoPlayEntrances = reachable.GroupBy(item => item.DestinationField).Select(group => group.OrderBy(item => SquaredDistance(before.X, before.Z, item.X, item.RawZ)).ThenBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.X).ThenBy(item => item.RawZ).First()).OrderBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        return autoPlayEntrances;

        void LoadSegment(int mapId)
        {
            if (!loadedSegments.Add(mapId))
            {
                return;
            }
            WorldSegment segment = assets.GetSegment(mapId, replacements);
            foreach (WorldTriangle triangle in segment.Triangles.Where(item => item.Walkable))
            {
                int index = terrain.Count;
                terrain.Add((triangle, new NativeWorldGroundIdentity(segment.MapId, segment.SourceId, triangle.NativeBlockIndex, triangle.NativePolygonIndex)));
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

    /// <summary>Relocates the debug-controlled foot pose; the ordinary callback still owns field entry and saved return state.</summary>
    public void DebugTeleportToEntrance(WorldAutoPlayTarget target)
    {
        RequireAutoPlayFoot();
        WorldState before = State;
        NativeFootWorldRuntimeState foot = before.NativeFootRuntime!;
        WorldGroundSelection ground = assets.LookupNativeGround(target.X, target.RawZ, replacements, []) ?? throw new InvalidOperationException("World teleport requires an actual native ground polygon.");
        ushort story = WorldEntryVariables.Read(session.State).StoryWord;
        WorldFieldSelection? selection = SelectAutoPlayEntrance(target.X, target.RawZ, ground.Ground.Triangle.TextureFlags, story);
        if (selection?.Entry.FieldId != target.DestinationField || !ground.Ground.Triangle.Walkable)
        {
            throw new InvalidOperationException("World teleport destination no longer resolves to the requested native entrance.");
        }
        int height = NativeWorldTerrain.ResolveHeight(foot.EntityType, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, before.NativeHeight).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity, foot.GroundCache);
        WorldState relocated = before with
        {
            X = target.X,
            Z = target.RawZ,
            NativeHeight = height,
            CameraVelocity = 0,
            CameraTargetX = target.X,
            CameraTargetZ = target.RawZ,
            CameraTargetNativeHeight = height - 256,
            GroundType = ground.Ground.Triangle.GroundType,
            NativeFootRuntime = foot with { GroundCache = cache, PreviousHorizontalInput = 0 }
        };
        EnsureContactCertificate(relocated);
        session.SetWorldState(relocated);
    }

    private WorldFieldSelection? SelectAutoPlayEntrance(int x, int rawZ, byte textureFlags, ushort story)
    {
        NativeSavedWorldState saved = session.State.SavedWorld ?? throw new NotSupportedException("World entrance discovery requires saved-world authority.");
        WorldState probe = State with { X = x, Z = rawZ };
        return assets.FieldEntries.Select(new WorldFieldSelectionInput(x, rawZ, story, State.NativeFootRuntime!.EntityType), textureFlags, (opcode, operand) => EvaluateWorldEventPredicate(probe, 0, saved, opcode, operand, eventUi: State.EventUi));
    }

    private void RequireAutoPlayFoot()
    {
        if (State.NativeFootRuntime is not { EntityType: 0 or 1, StationTrains: null, NormalObjects: null } || State.NativeCarRuntime is not null || State.NativeGardenRuntime is not null || State.NativeRagnarokRuntime is not null || State.NativeTrainRuntime is not null || State.NativeScriptedRuntime is not null)
        {
            throw new NotSupportedException("Automatic coordinate travel currently requires ordinary foot with no vehicle, rail or auxiliary object callback owner.");
        }
        State.NativeFootRuntime.RequireNativeGroundState();
    }

    private static long SquaredDistance(int x, int z, int targetX, int targetZ)
    {
        long dx = targetX - (long)x;
        long dz = targetZ - (long)z;
        return dx * dx + dz * dz;
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
