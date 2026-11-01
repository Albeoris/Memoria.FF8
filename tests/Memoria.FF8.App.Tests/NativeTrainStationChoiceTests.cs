using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTrainStationChoiceTests
{
    [Theory]
    [InlineData("App.Tests.NativeTrainStationChoiceTests.ProximityBoundary")]
    [InlineData("App.Tests.NativeTrainStationChoiceTests.VisibilityBoundary")]
    public void FootMovementAcrossTrainCameraBoundariesRestoresTheNextCallback(string fixture)
    {
        GameState source = GameSession.Load(TestResources.FixturePath(fixture));
        var session = new GameSession(source);
        using var world = new GameWorld(session, GameDirectory());
        Assert.True(world.EnsureField());
        long before = session.State.World!.Frame;
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1, Vertical: -1, Frames: 8);
        PlayerActionResult result = world.Apply(move);
        Assert.True(result.Accepted);
        Assert.Equal(before + 8, session.State.World!.Frame);

        string path = TestResources.ArtifactFile("station-proximity.ff8");
        session.Save(path, compact: true);
        var restoredSession = new GameSession(GameSession.Load(path));
        using var restored = new GameWorld(restoredSession, GameDirectory());
        Assert.True(restored.EnsureField());
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.PrepareSnapshot()), JsonSerializer.SerializeToElement(restoredSession.PrepareSnapshot())));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void InvalidVisibleChoicePreservesTheCompleteLoadedSession(int choice)
    {
        var session = new GameSession(LoadStationPrompt());
        using var world = new GameWorld(session, GameDirectory());
        Assert.True(world.EnsureField());
        AssertStationPrompt(world);
        JsonElement before = JsonSerializer.SerializeToElement(session.State);
        JsonElement dormantField = world.Events.Capture();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: choice)));

        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(session.State)));
        Assert.True(JsonElement.DeepEquals(dormantField, world.Events.Capture()));
        AssertStationPrompt(world);
    }

    [Fact]
    public void DismountUsesTheOriginalStationPoseAndRestoresTheNextFootCallbackAndPixels()
    {
        var session = new GameSession(LoadStationPrompt());
        using var world = new GameWorld(session, GameDirectory());
        world.EnsureField();
        AssertStationPrompt(world);
        WorldState before = session.State.World!;
        WorldTransition packet = session.State.WorldArrival!;

        PlayerActionResult dismount = world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: 1));

        Assert.True(dismount.Accepted);
        Assert.Equal(1, dismount.FramesAdvanced);
        Assert.Null(world.CurrentDialogue);
        WorldState station = session.State.World!;
        Assert.Equal(before.Frame + 1, station.Frame);
        Assert.Equal((-28338, -23747, -794, 0), (station.X, station.Z, station.NativeHeight, station.BodyYaw));
        Assert.Equal(packet, session.State.WorldArrival);
        Assert.Null(station.NativeTrainRuntime);
        NativeFootWorldRuntimeState foot = Assert.IsType<NativeFootWorldRuntimeState>(station.NativeFootRuntime);
        Assert.Equal(NativeFootWorldRuntimeState.TrainDismountRule, foot.Rule);
        Assert.Equal((byte)0, foot.EntityType);
        Assert.Equal(station.Frame, foot.TrainDismount!.SourceCallback);
        Assert.Equal(0, foot.TrainDismount.Station.Index);
        Assert.Equal(station.Frame, foot.StationTrainProcess!.Callback);
        Assert.Equal(0, foot.StationTrainProcess.Primary.Pause);

        var restoredSession = new GameSession(Clone(session.State));
        using var restored = new GameWorld(restoredSession, GameDirectory());
        restored.EnsureField();
        world.SetRenderSize(320, 224);
        restored.SetRenderSize(320, 224);
        AssertSameState(session, restoredSession);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));

        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.Equal(station.Frame + 1, session.State.World!.Frame);
        Assert.NotNull(session.State.World.NativeFootRuntime!.StationTrainProcess!.ProjectionCamera);
        Assert.Equal(session.State.World.Frame, session.State.World.NativeFootRuntime.StationTrainProcess.Callback);
        AssertSameState(session, restoredSession);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    [Fact]
    public void DecliningDismountReleasesTheSameTrainBankAndResumesItsNextCallback()
    {
        var session = new GameSession(LoadStationPrompt());
        using var world = new GameWorld(session, GameDirectory());
        world.EnsureField();
        AssertStationPrompt(world);
        long sourceFrame = session.State.World!.Frame;

        PlayerActionResult decline = world.Apply(new PlayerAction(PlayerActionKind.Choose, Value: 2));

        Assert.True(decline.Accepted);
        Assert.Equal(1, decline.FramesAdvanced);
        Assert.Null(world.CurrentDialogue);
        Assert.Null(session.State.World!.NativeFootRuntime);
        NativeTrainWorldRuntimeState train = Assert.IsType<NativeTrainWorldRuntimeState>(session.State.World.NativeTrainRuntime);
        Assert.Equal(sourceFrame + 1, train.Callback);
        Assert.Equal(0, train.Primary.Pause);
        Assert.Contains(train.Callback, train.StationDeclines);

        var restoredSession = new GameSession(Clone(session.State));
        using var restored = new GameWorld(restoredSession, GameDirectory());
        restored.EnsureField();
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.Equal(train.Callback + 1, session.State.World.NativeTrainRuntime!.Callback);
        Assert.Null(world.CurrentDialogue);
        AssertSameState(session, restoredSession);
    }

    [Fact]
    public void StationFootCanReboardItsSavedCarWhileRetainingTheCurrentRailProcess()
    {
        string directory = GameDirectory();
        using WorldAssets assets = new(directory);
        GameState source = CreateCurrentStationPrompt();
        NativeTrainWorldRuntimeState train = source.World!.NativeTrainRuntime!;
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(source.World.ReplacementMask);
        // Declared roadside component inputs exercise the transfer after an actual
        //548AF0 dismount; no travel route or gameplay checkpoint is fabricated.
        const int carX = -21894;
        const int carZ = -41257;
        WorldGroundSelection carGround = assets.LookupNativeGround(carX, carZ, replacements, [])!;
        short carHeight = checked((short)NativeWorldTerrain.ResolveHeight(38, -(int)carGround.Ground.Height, carGround.Ground.Triangle.GroundType, 0).Height);
        NativeSavedWorldVehicleSlot slot = new(85, new(carX, carZ, carHeight, 3008));
        train = train with { Load = train.Load with { Slot4 = slot } };
        WorldState precedingFoot = source.DepartingWorldState! with { NativeCarRuntime = null, NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame()) };
        NativeSavedWorldFootReturn footReturn = NativeSavedWorldFootReturn.RecordFootDeparture(precedingFoot, 262, 290, 1);
        NativeSavedWorldState parkedSaved = (source.SavedWorld! with { CarReturn = null, Slot4 = slot }).WithFootReturn(footReturn);
        source = source with { World = source.World with { NativeTrainRuntime = train }, DepartingWorldState = precedingFoot, SavedWorld = parkedSaved };
        GameSession session = new(source);
        using WorldMapScene scene = new(session, directory);
        Assert.True(scene.Apply(new(PlayerActionKind.Choose, Value: 1)).Accepted);
        Assert.NotNull(scene.State.NativeFootRuntime!.StationTrains);
        const int footX = carX + 256;
        WorldGroundSelection ground = assets.LookupNativeGround(footX, carZ, replacements, [])!;
        int height = NativeWorldTerrain.ResolveHeight(0, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, 0).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        WorldState dismounted = scene.State;
        session.SetWorldState(dismounted with
        {
            X = footX,
            Z = carZ,
            NativeHeight = height,
            CameraYaw = 3046,
            CameraTargetX = footX,
            CameraTargetZ = carZ,
            CameraTargetNativeHeight = height - 256,
            GroundType = ground.Ground.Triangle.GroundType,
            NativeFootRuntime = dismounted.NativeFootRuntime! with { GroundCache = cache, GroundYaw = 0, CameraTransition = new(0, 1024) }
        });
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        GameSession restored = new(Clone(session.State));
        using WorldMapScene replay = new(restored, directory);
        long callback = scene.State.NativeFootRuntime!.StationTrainProcess!.Callback;
        Assert.Equal(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)), replay.Apply(new(PlayerActionKind.WorldButtons, 0x40)));
        NativeCarWorldRuntimeState boarded = Assert.IsType<NativeCarWorldRuntimeState>(scene.State.NativeCarRuntime);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((byte)38, boarded.EntityType);
        Assert.Equal(callback + 1, boarded.ObjectProcess!.Callback);
        Assert.NotNull(boarded.ObjectProcess.RidingTrainSource);
        Assert.NotNull(boarded.Forest);
        Assert.Equal((carX, carZ), (scene.State.X, scene.State.Z));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        GameSession carRestore = new(Clone(session.State));
        using WorldMapScene carReplay = new(carRestore, directory);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), carReplay.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(carRestore.State)));
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(1, 0, -1, 0)]
    [InlineData(2, 1, 1, 1)]
    public void DismountContractRetainsProcessOwnersForEachOriginalStation(int stationIndex, int trackIndex, sbyte direction, byte stopCounter)
    {
        using WorldAssets assets = new(GameDirectory());
        GameState boundary = CreateCurrentStationPrompt();
        NativeTrainWorldRuntimeState source = boundary.World!.NativeTrainRuntime!;
        NativeTrainRailBank primary = trackIndex == 0 ? source.Primary : source.Alternate!;
        NativeTrainRailBank? alternate = trackIndex == 0 ? source.Alternate : source.Primary;
        // Declared548BF0 status-word component inputs test548AF0's station selection;
        // this does not claim these banks were reached through a complete train journey.
        NativeTrainWorldRuntimeState train = source with
        {
            Packet = new(checked((short)(trackIndex == 0 ? 64 : 66)), source.EntityType, -1),
            CameraListIndex = trackIndex,
            Primary = primary with { Status = 1, Direction = direction, StopCounter = stopCounter, Pause = -1 },
            Alternate = alternate
        };
        NativeWorldTrainStation station = assets.ReadTrainStation(stationIndex);
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateTrainDismount(train, station, train.CameraGeometry.Pitch, boundary.SavedWorld!);
        foot.RequireNativeGroundState();
        Assert.Equal(stationIndex, foot.TrainDismount!.Station.Index);
        Assert.Equal(train.ControlledObjectEntityType, foot.EntityType);
        Assert.Same(train.GroundCache, foot.GroundCache);
        Assert.Equal(train.GroundYaw, foot.GroundYaw);
        Assert.Equal(train.Forest.RandomCursor, foot.Forest!.RandomCursor);
        Assert.Equal(train.Callback, foot.StationTrainProcess!.Callback);
        Assert.Equal((short)0, foot.StationTrainProcess.Primary.Pause);
        Assert.True(train.Primary.Nodes.SequenceEqual(foot.StationTrainProcess.Primary.Nodes));
        NativeFootWorldRuntimeState restored = JsonSerializer.Deserialize<NativeFootWorldRuntimeState>(JsonSerializer.Serialize(foot))!;
        restored.RequireNativeGroundState();
        Assert.Equal(stationIndex, restored.TrainDismount!.Station.Index);
    }

    private static GameState CreateCurrentStationPrompt()
    {
        string directory = GameDirectory();
        using GameAssets initialization = new(directory);
        GameSession session = NativeTrainRideTests.CreateComponent(initialization);
        WorldTransition packet = new(64, 16, -1);
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, prepared.InitialState, session.WorldControls!, prepared.ValidateAdoption, prepared.InitialSavedWorld);
        using WorldMapScene scene = prepared.Adopt(session);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 400)).Accepted);
        Assert.Equal((short)-1, scene.State.NativeTrainRuntime!.Primary.Pause);
        Assert.InRange(scene.State.Frame, 1, 400);
        Assert.Equal(4, scene.CurrentDialogue!.MessageId);
        return session.State;
    }

    private static void AssertStationPrompt(GameWorld world)
    {
        Assert.NotNull(world.CurrentDialogue);
        Assert.Equal(4, world.CurrentDialogue.MessageId);
        Assert.Equal(0x5484B0, world.CurrentDialogue.Operation);
        Assert.Equal((1, 2), (world.CurrentDialogue.FirstChoice, world.CurrentDialogue.LastChoice));
        Assert.Equal(-1, world.WorldScene!.State.NativeTrainRuntime!.Primary.Pause);
    }

    private static void AssertSameState(GameSession expected, GameSession actual)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.State), JsonSerializer.SerializeToElement(actual.State)));
    }

    private static GameState Clone(GameState state)
    {
        return JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
    }

    private static GameState LoadStationPrompt()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        GameState source = GameSession.Load(TestResources.FixturePath("App.Tests.NativeTrainStationChoiceTests.1"));
        return source;
    }

    private static string GameDirectory()
    {
        return TestResources.GameDirectory;
    }
}
