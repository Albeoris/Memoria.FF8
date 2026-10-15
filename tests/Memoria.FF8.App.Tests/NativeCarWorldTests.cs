using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarWorldTests
{
    [Fact]
    public void Native_alternate_ground_selection_retains_the_original_candidate_yaw_and_cache()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        using var assets = new WorldAssets(directory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(285);
        // Installed edge geometry fixture; this cache is deliberately local and does
        // not stand in for the authentic route47 callback history.
        NativeWorldGroundIdentity initial = assets.LookupNativeGround(12910, -27143, replacements, [])!.Identity;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(initial) with { Alternatives = [new(0, [], initial, initial) { FailedSelections = 0 }] };
        WorldNativeGroundResult selected = assets.ResolveNativeGround(12910, -27143, -6, -8, 304, 0, cache, replacements);
        Assert.False(selected.Blocked);
        Assert.InRange(selected.GroundYaw, -1120, -160);
        Assert.NotEqual((12904, -27135), (selected.X, selected.Z));
        NativeWorldGroundAlternative next = Assert.Single(selected.Cache.Alternatives);
        Assert.Equal(0, next.FailedSelections);
        Assert.Equal(selected.Center!.Identity, next.CurrentGround);
        Assert.Equal(initial, next.PreviousGround);
        Assert.NotEmpty(next.Entries);
        Assert.Empty(cache.Alternatives[0].Entries);
        NativeWorldGroundIdentity edge = assets.LookupNativeGround(12904, -27135, replacements, [])!.Identity;
        NativeWorldGroundCacheState stoppedCache = NativeWorldGroundCacheState.CreateFresh(edge) with { Alternatives = [new(0, [], edge, edge) { FailedSelections = 0 }] };
        WorldNativeGroundResult blocked = assets.ResolveNativeGround(12904, -27135, 0, 0, 304, 0, stoppedCache, replacements);
        Assert.True(blocked.Blocked);
        Assert.Equal((12904, -27135, 0), (blocked.X, blocked.Z, blocked.GroundYaw));
        Assert.Equal(1, Assert.Single(blocked.Cache.Alternatives).FailedSelections);
        NativeWorldGroundCacheState unknownFailure = stoppedCache with { Alternatives = [stoppedCache.Alternatives[0] with { FailedSelections = null }] };
        WorldNativeGroundResult unanimousBlocked = assets.ResolveNativeGround(12904, -27135, 0, 0, 304, 0, unknownFailure, replacements);
        Assert.True(unanimousBlocked.Blocked);
        Assert.Equal((12904, -27135), (unanimousBlocked.X, unanimousBlocked.Z));
        Assert.Equal(2, unanimousBlocked.Cache.Alternatives.Length);
        Assert.All(unanimousBlocked.Cache.Alternatives, alternative => Assert.Null(alternative.FailedSelections));
        NativeWorldGroundCacheState wrappingFailure = stoppedCache with { Alternatives = [stoppedCache.Alternatives[0] with { FailedSelections = int.MaxValue }] };
        WorldNativeGroundResult wrappedBlocked = assets.ResolveNativeGround(12904, -27135, 0, 0, 304, 0, wrappingFailure, replacements);
        Assert.Equal(int.MinValue, Assert.Single(wrappedBlocked.Cache.Alternatives).FailedSelections);

        Assert.Equal(edge, blocked.Cache.Alternatives[0].CurrentGround);
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 40, [265] = 128 }) });
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { X = 12904, Z = -27135, NativeHeight = -304, CameraTargetX = 12904, CameraTargetZ = -27135, CameraTargetNativeHeight = -560, GroundType = assets.ResolveNativeGroundIdentity(edge, replacements).GroundType, NativeCarRuntime = scene.State.NativeCarRuntime! with { GroundCache = stoppedCache, Speed = 2 } });
        session.SetExamTimer(NativeExamTimerState.FromReachedSet(600).KillDisplay());
        GameState before = session.State;
        Assert.Equal(1, scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
        Assert.Equal((12904, -27135), (scene.State.X, scene.State.Z));
        Assert.Equal(2287, scene.State.BodyYaw);
        Assert.Equal(0, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(2, session.ExamTimer!.ClockCalls);
        Assert.Equal(1, scene.State.Frame);
        var replaySession = new GameSession(before);
        using var replay = new WorldMapScene(replaySession, directory);
        Assert.Equal(1, replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
    }

    [Fact]
    public void Original_overlap_lookup_obeys_retained_polygon_precedence_without_populating_or_reordering_the_cache()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        using var assets = new WorldAssets(directory);
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 0));
        // Geometry/query fixture in the actual route43 segment. These local pointer
        // alternatives test the native lookup rule, not an authenticated player cache.
        WorldGroundSelection[]? overlap = null;
        int selectedX = 0;
        int selectedZ = 0;
        WorldSegment original = assets.GetSegment(WorldAssets.SegmentAt(17151, -28610), replacements);
        foreach ((int x, int z) in original.Triangles.Where(triangle => (triangle.VertexFlags & 0x40) != 0).SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).Select(vertex => ((int)vertex.X, (int)vertex.Z)).Distinct())
        {
            WorldSegment segment = assets.GetSegment(WorldAssets.SegmentAt(x, z), replacements);
            WorldGroundSelection[] containing = segment.Triangles.Select(triangle => new NativeWorldGroundIdentity(segment.MapId, segment.SourceId, triangle.NativeBlockIndex, triangle.NativePolygonIndex)).Select(identity => assets.LookupNativeGround(x, z, replacements, [identity])).Where(selected => selected is not null).Select(selected => selected!).DistinctBy(selected => selected.Identity).Where(selected => (selected.Ground.Triangle.VertexFlags & 0x40) != 0).ToArray();
            if (containing.Length > 1)
            {
                overlap = containing;
                selectedX = x;
                selectedZ = z;
                break;
            }
        }
        Assert.NotNull(overlap);
        NativeWorldGroundIdentity[] cache = overlap.Select(selected => selected.Identity).ToArray();
        Assert.Equal(cache[0], assets.LookupNativeGround(selectedX, selectedZ, replacements, cache)!.Identity);
        NativeWorldGroundIdentity[] reversed = cache.Reverse().ToArray();
        Assert.Equal(reversed[0], assets.LookupNativeGround(selectedX, selectedZ, replacements, reversed)!.Identity);
        Assert.Equal(overlap.Select(selected => selected.Identity), cache);
        Assert.Equal(overlap.Reverse().Select(selected => selected.Identity), reversed);
    }

    [Fact]
    public void Original_car_spawn_controls_mesh_and_independent_restore_preserve_native_vehicle_authority()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        // Isolated component fixture; actual gameplay acceptance belongs to the authentic New Game journal.
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 40, [265] = 128 }) });
        using var scene = new WorldMapScene(session, directory);
        Assert.Equal((24511, -28167, -552), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((2256, 2256), (scene.State.CameraYaw, scene.State.BodyYaw));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(73, session.State.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(0, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(2, scene.State.NativeCarRuntime.GroundCache!.Alternatives.Length);
        Assert.All(scene.State.NativeCarRuntime.GroundCache.Alternatives, alternative =>
        {
            Assert.Empty(alternative.Entries);
            Assert.Equal(alternative.CurrentGround, alternative.PreviousGround);
        });
        using var assets = new WorldAssets(directory);
        Assert.Equal(73, assets.ExamCar.ShapeId);
        Assert.Equal(9, assets.ExamCar.ModelIndex);
        Assert.Equal(84, assets.ExamCar.NativeVertices.Count);
        Assert.Equal(4, assets.ExamCar.Faces.Count(face => face.Vertices.Length == 3));
        Assert.Equal(54, assets.ExamCar.Faces.Count(face => face.Vertices.Length == 4));
        Assert.Equal(2, assets.ExamCar.Textures.Count);
        Assert.All(assets.ExamCar.Textures, texture => Assert.Equal((64, 64), (texture.Width, texture.Height)));
        Assert.Equal(8, assets.ExamCar.Faces.Count(face => face.Palette == 0));
        Assert.Equal(50, assets.ExamCar.Faces.Count(face => face.Palette == 1));
        GameState before = session.State;
        byte[] first = scene.Render(320, 224).ToArray();
        Assert.True(scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: -1)).Accepted);
        Assert.Equal(2, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(2287, scene.State.BodyYaw);
        NativeWorldGroundAlternative cache = Assert.Single(scene.State.NativeCarRuntime.GroundCache!.Alternatives);
        Assert.Equal(1, cache.PreferredGroup);
        Assert.NotEmpty(cache.Entries);
        Assert.InRange(cache.Entries.Length, 1, 12);
        ReplayJournalEntry action = session.ReplayJournal!.Endpoint!;
        var replaySession = new GameSession(before);
        using var replay = new WorldMapScene(replaySession, directory);
        Assert.Equal(1, replay.Apply(new PlayerAction((PlayerActionKind)action.Code, action.Value, action.Text, action.Horizontal, action.Vertical, action.Frames)).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        GameState endpoint = session.State;
        using var restored = new WorldMapScene(new GameSession(endpoint), directory);
        Assert.Equal(scene.Render(320, 224), restored.Render(320, 224));
        Assert.NotEqual(first, scene.Render(320, 224));
        var next = new PlayerAction(PlayerActionKind.Wait);
        Assert.Equal(scene.Apply(next), restored.Apply(next));
        Assert.Equal(0, scene.State.NativeCarRuntime.Speed);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(scene.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Throws<InvalidDataException>(() => (scene.State.NativeCarRuntime with { GroundCache = null }).Validate());
    }

    [Fact]
    public void Native_car_field_entry_selects_the_actual_entrance_polygon_and_atomically_retires_shape73()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 40, [265] = 128 }) });
        using (var entry = new WorldMapScene(session, directory))
        {
            // Isolated component contact fixture at an installed entrance-polygon centroid;
            // this neither records a causal route nor establishes ordinary player coverage.
            using var assets = new WorldAssets(directory);
            NativeWorldGroundIdentity ground = assets.LookupNativeGround(12390, -26709, WorldTerrainSelection.Replacements(entry.State.ReplacementMask), [])!.Identity;
            session.SetWorldState(entry.State with { X = 12390, Z = -26709, NativeHeight = -301, CameraTargetX = 12390, CameraTargetZ = -26709, CameraTargetNativeHeight = -557, NativeCarRuntime = entry.State.NativeCarRuntime! with { GroundCache = NativeWorldGroundCacheState.CreateFresh(ground) } });
        }
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(106, session.State.FieldId);
        Assert.Equal(new FieldTransition(106, 58, 4327, 197, 0), session.State.FieldArrival);
        Assert.Equal(262, session.State.PreviousFieldId);
        Assert.Null(session.State.World);
        NativeSavedWorldCarReturn receipt = session.State.SavedWorld!.CarReturn!;
        Assert.Equal(33, receipt.EntityType105);
        Assert.Equal(255, session.State.SavedWorld.Slot4.ShapeId);
        Assert.Equal(receipt.Pose, session.State.SavedWorld.Slot4.Pose);
        Assert.Null(session.State.SavedWorld.FootReturn);
        Assert.Equal(1, receipt.SourceWorldFrame);
        Assert.Equal(40, receipt.StoryWord);
        Assert.NotNull(session.State.DepartingWorldState!.NativeCarRuntime);
        Assert.Equal(33, session.WorldConfiguration!.EntityByte);
        Assert.Equal(NativeWorldConfiguration.ReturnRule, session.WorldConfiguration.Rule);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-car-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path, registerJournalReference: false);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(GameSession.Load(path))));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Refused_car_batch_does_not_commit_partial_pose_speed_timer_or_journal_changes()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 40, [265] = 128 }) });
        session.SetExamTimer(NativeExamTimerState.FromReachedSet(600).KillDisplay());
        using var scene = new WorldMapScene(session, directory);
        session.SetWorldState(scene.State with { X = -5000, CameraTargetX = -1476, CameraYaw = 0 });
        GameState before = session.State;
        Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 20)));
        Assert.Same(before, session.State);
        Assert.Equal(0, session.ExamTimer!.ClockCalls);
    }

    [Fact]
    public void Prepared_car_entry_saved_slot_writes_are_validated_before_live_publication()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { FieldId = 262, EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 40, [265] = 128 }) });
        var transition = new WorldTransition(63, 33, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, transition);
        GameState before = session.State;
        Assert.Equal(0, before.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(73, ticket.InitialSavedWorld!.Slot4.ShapeId);
        Assert.Throws<InvalidOperationException>(() => session.CommitPreparedWorldEntry(transition, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption));
        Assert.Same(before, session.State);
        session.CommitPreparedWorldEntry(transition, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.Equal(73, session.State.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(33, scene.State.NativeCarRuntime!.EntityType);
        Assert.Throws<InvalidOperationException>(() => ticket.Adopt(session));
    }

    private static void InitializeNativeOwners(GameSession session, string directory)
    {
        // Declared car components still execute the real cold/NewGame owners and
        // decode physical dream/kill/economy/table values from the installed corpus.
        session.StartNewGame();
        using var assets = new GameAssets(directory);
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
    }
}
