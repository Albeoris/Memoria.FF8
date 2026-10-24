using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarWorldTests
{
    [Fact]
    public void FullMapAdvancesTheCarMotorButRetainsGroundPositionAndSaveNormalization()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128) });
        // Declared component input: the original first-car tutorial flag has already been set.
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = session.State.SavedWorld.WorldEventFlags116!.WithOperand(0x100) } });
        using WorldMapScene scene = new(session, directory);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0, Frames: 24)).Accepted);
        foreach (byte mode in new byte[] { 1, 2 })
        {
            Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0)).Accepted);
            Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x100)).Accepted);
            Assert.Equal(mode, WorldMapScene.MapDisplayMode(scene.State));
            Assert.Equal(mode, NativeSavedWorldCarReturn.Create(scene.State, 262, 40, 0).MapDisplayMode110);
        }
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0)).Accepted);
        WorldState before = scene.State;
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x180)).Accepted);
        Assert.Equal((before.X, before.Z, before.NativeHeight), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal(2, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(before.NativeCarRuntime!.MovementMagnitude20409F4, scene.State.NativeCarRuntime.MovementMagnitude20409F4);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.NativeCarRuntime.GroundCache), JsonSerializer.SerializeToElement(scene.State.NativeCarRuntime.GroundCache)));
        Assert.Equal((byte)0, NativeSavedWorldCarReturn.Create(scene.State, 262, 40, 0).MapDisplayMode110);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        PlayerAction held = new(PlayerActionKind.WorldButtons, 0x180);
        Assert.True(scene.Apply(held).Accepted);
        Assert.True(replay.Apply(held).Accepted);
        Assert.Equal(0, scene.State.NativeCarRuntime.Speed);
        Assert.Equal((before.X, before.Z), (scene.State.X, scene.State.Z));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    [Fact]
    public void Native_alternate_ground_selection_retains_the_original_candidate_yaw_and_cache()
    {
        string directory = TestResources.GameDirectory;
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
        Assert.NotEmpty(unanimousBlocked.Cache.Alternatives);
        Assert.All(unanimousBlocked.Cache.Alternatives, alternative => Assert.Null(alternative.FailedSelections));
        NativeWorldGroundCacheState wrappingFailure = stoppedCache with { Alternatives = [stoppedCache.Alternatives[0] with { FailedSelections = int.MaxValue }] };
        WorldNativeGroundResult wrappedBlocked = assets.ResolveNativeGround(12904, -27135, 0, 0, 304, 0, wrappingFailure, replacements);
        Assert.Equal(int.MinValue, Assert.Single(wrappedBlocked.Cache.Alternatives).FailedSelections);

        Assert.Equal(edge, blocked.Cache.Alternatives[0].CurrentGround);
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128) });
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
        string directory = TestResources.GameDirectory;
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
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        // Isolated component fixture; actual gameplay acceptance belongs to the live New Game journey.
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128) });
        using var scene = new WorldMapScene(session, directory);
        Assert.Equal((24511, -28167, -552), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal((2256, 2256), (scene.State.CameraYaw, scene.State.BodyYaw));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(73, session.State.SavedWorld!.Slot4.ShapeId);
        Assert.Equal(0, scene.State.NativeCarRuntime!.Speed);
        Assert.Equal(0, Assert.Single(scene.State.NativeCarRuntime.GroundCache!.Alternatives).PreferredGroup);
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
        Assert.Equal(0, cache.PreferredGroup);
        Assert.NotEmpty(cache.Entries);
        Assert.InRange(cache.Entries.Length, 1, 12);
        var replaySession = new GameSession(before);
        using var replay = new WorldMapScene(replaySession, directory);
        Assert.Equal(1, replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: -1)).FramesAdvanced);
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
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128) });
        using (var entry = new WorldMapScene(session, directory))
        {
            // Isolated component contact fixture at an installed entrance-polygon centroid;
            // this neither records a causal route nor establishes ordinary player coverage.
            using var assets = new WorldAssets(directory);
            NativeWorldGroundIdentity ground = assets.LookupNativeGround(12390, -26709, WorldTerrainSelection.Replacements(entry.State.ReplacementMask), [])!.Identity;
            session.SetWorldState(entry.State with { X = 12390, Z = -26709, NativeHeight = -301, CameraTargetX = 12390, CameraTargetZ = -26709, CameraTargetNativeHeight = -557, NativeCarRuntime = entry.State.NativeCarRuntime! with { GroundCache = NativeWorldGroundCacheState.CreateFresh(ground) } });
        }
        // This component checks entrance selection and the production entry transaction;
        // it does not load a destination field VM or its audio/presentation backend.
        using var world = new WorldMapScene(session, directory);
        bool validatedPreparedEntry = false;
        world.PrepareFieldEntry = (selection, final, location, salary) =>
        {
            FieldTransition transition = new(selection.Entry.FieldId, selection.Entry.X, selection.Entry.Y, selection.Entry.Triangle, selection.Entry.Direction);
            NativeSavedWorldCarReturn prepared = NativeSavedWorldCarReturn.Create(final, checked((ushort)session.State.FieldId), session.State.WorldEntryMemory!.StoryWord, location);
            GameState departure = session.State;
            Assert.Throws<InvalidOperationException>(() => session.CommitPreparedCarFieldEntry(departure, final, transition, prepared, salary, _ => throw new InvalidOperationException("Declared destination preparation refusal.")));
            Assert.Same(departure, session.State);
            return () => session.CommitPreparedCarFieldEntry(departure, final, transition, prepared, salary, candidate =>
            {
                Assert.Same(departure, session.State);
                Assert.Equal(255, candidate.SavedWorld!.Slot4.ShapeId);
                Assert.Equal(106, candidate.FieldId);
                validatedPreparedEntry = true;
            });
        };
        PlayerActionResult guard = world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 24));
        Assert.Equal(PlayerTriggerKind.Dialogue, guard.Trigger.Kind);
        Assert.Equal(49, guard.Trigger.MessageId);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.True(validatedPreparedEntry);
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
        Assert.Equal(25, receipt.SourceWorldFrame);
        Assert.Equal(40, receipt.StoryWord);
        Assert.NotNull(session.State.DepartingWorldState!.NativeCarRuntime);
        Assert.Equal(33, session.WorldConfiguration!.EntityByte);
        Assert.Equal(NativeWorldConfiguration.ReturnRule, session.WorldConfiguration.Rule);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-car-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(GameSession.Load(path))));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Refused_car_batch_does_not_commit_partial_pose_speed_timer_or_snapshot_changes()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, FieldId = 262, WorldArrival = new WorldTransition(63, 33, -1), FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128) });
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
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        var session = GameSession.CreateColdProcess();
        InitializeNativeOwners(session, directory);
        session.Restore(session.State with { FieldId = 262, FieldLoadContext = null, WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40).WriteBytes(265, 1, 128) });
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
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
    }
}
