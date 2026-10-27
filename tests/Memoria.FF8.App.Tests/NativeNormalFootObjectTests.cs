using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Declared native components using installed original resources; these fixtures do not represent an authentic player journey.</summary>
public sealed class NativeNormalFootObjectTests
{
    [Fact]
    public void FullMapContinuesNormalObjectsAndRestoresBeforeReturningToTravel()
    {
        VerifyFullMap(ComponentSession());
    }

    [Fact]
    public void SuppliedStandaloneContinuesNormalObjectsThroughFullMap()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("normal-foot-map-source")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.NotNull(session.State.World!.NativeFootRuntime!.NormalObjects);
        Assert.NotEqual((byte)3, WorldMapScene.MapDisplayMode(session.State.World));
        do
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(world.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
            world.Render(TimeSpan.Zero);
        }
        while (WorldMapScene.MapDisplayMode(session.State.World!) != 3);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).Accepted);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_NORMAL_FOOT_MAP_OUTPUT") ?? TestResources.ArtifactFile("normal-foot-map.json");
        session.Save(output, compact: true);
        GameSession savedSession = new(GameSession.Load(output));
        using GameWorld saved = new(savedSession, TestResources.GameDirectory);
        saved.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), saved.Render(TimeSpan.Zero));
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait, WorldButtons: 0x100), new(PlayerActionKind.Wait), new(PlayerActionKind.Move, Horizontal: 1) })
        {
            Assert.Equal(world.Apply(action), saved.Apply(action));
            Assert.Equal(JsonSerializer.Serialize(session.PrepareSnapshot()), JsonSerializer.Serialize(savedSession.PrepareSnapshot()));
            Assert.Equal(world.Render(TimeSpan.Zero), saved.Render(TimeSpan.Zero));
        }
        Assert.Equal((byte)0, WorldMapScene.MapDisplayMode(session.State.World!));
    }

    private static void VerifyFullMap(GameSession session)
    {
        using WorldMapScene world = new(session, TestResources.GameDirectory);
        Assert.NotNull(world.State.NativeFootRuntime!.NormalObjects);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        world.Render(320, 224);
        byte initialMode = WorldMapScene.MapDisplayMode(world.State);
        Assert.NotEqual((byte)3, initialMode);
        do
        {
            Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(world.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
            world.Render(320, 224);
        }
        while (WorldMapScene.MapDisplayMode(world.State) != 3);
        WorldState before = world.State;
        NativeNormalFootObjectProcess objects = before.NativeFootRuntime!.NormalObjects!;
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 1, Frames: 3)).Accepted);
        Assert.Equal((before.X, before.Z, before.NativeHeight), (world.State.X, world.State.Z, world.State.NativeHeight));
        Assert.Equal(objects.Callback + 3, world.State.NativeFootRuntime!.NormalObjects!.Callback);
        Assert.Equal(world.State.Frame, world.State.NativeFootRuntime.NormalObjects.Callback);
        Assert.Equal(world.State.CameraYaw, world.State.NativeFootRuntime.NormalObjects.Camera.Yaw);
        byte[] pixels = world.Render(320, 224);
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
        Assert.Equal(pixels, restored.Render(320, 224));
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait, WorldButtons: 0x100), new(PlayerActionKind.Wait), new(PlayerActionKind.Move, Horizontal: 1) })
        {
            Assert.Equal(world.Apply(action), restored.Apply(action));
            Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restoredSession.State));
            Assert.Equal(world.Render(320, 224), restored.Render(320, 224));
        }
        Assert.Equal((byte)0, WorldMapScene.MapDisplayMode(world.State));
    }

    [Theory]
    [InlineData(290, 3, 9)]
    [InlineData(315, 3, 9)]
    [InlineData(344, 3, 9)]
    [InlineData(345, 4, 13)]
    [InlineData(347, 4, 13)]
    [InlineData(348, 5, 1)]
    [InlineData(352, 5, 1)]
    [InlineData(391, 5, 1)]
    public void OriginalSelectedClauseRetainsEveryDenseObjectAndBothFreshRailBanks(ushort story, int clause, int count)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(story, 0);
        NativeTrainRailBank primary = NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1);
        NativeTrainRailBank alternate = NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1);
        var camera = new NativeWorldFootCameraState(-32742, -26874, -755, -32742, -26874, -1011, 3760, -341, -341, -7168);
        var process = new NativeNormalFootObjectProcess(1, allocation, 0, primary, alternate, camera, uint.MaxValue, uint.MaxValue);
        process.Validate();
        Assert.Equal(clause, allocation.ClauseIndex);
        Assert.Equal(count, allocation.Objects.Length);
        Assert.Equal(0, allocation.ControlledObjectIndex);
        Assert.All(new[] { primary, alternate }, bank =>
        {
            Assert.Equal(30, bank.Pause);
            Assert.Equal(0, bank.Speed);
            Assert.Null(bank.Status);
            Assert.Equal(1, bank.Direction);
            Assert.All(bank.Presentation, pose => Assert.Null(pose.Projection));
        });
        Assert.Throws<InvalidDataException>(() => (process with { PrimaryDistanceSquared = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (process with { Alternate = null, AlternateDistanceSquared = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (process with { Allocation = assets.ReadTrainWorldAllocation(story, 1), Alternate = null, AlternateDistanceSquared = null }).Validate());
        Assert.Throws<NotSupportedException>(() => assets.ReadFootAllocation(0, story));
    }

    [Fact]
    public void PriorProximityWordSelectsNativeThirtyTwoZeroCycleWithoutChangingModeThreeDefault()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(0);
        NativeTrainRailBank initial = NativeTrainRailBank.Create(definition, 1);
        Assert.Null(NativeNormalFootObjectProcess.SelectSpeed(initial, 0, 48));
        NativeTrainRailBank unpaused = initial with { Pause = 0 };
        Assert.Null(NativeNormalFootObjectProcess.SelectSpeed(unpaused, uint.MaxValue, 48));
        Assert.Equal(32, NativeNormalFootObjectProcess.SelectSpeed(unpaused, 0, 48));
        Assert.Equal(0, NativeNormalFootObjectProcess.SelectSpeed(unpaused with { Speed = 32 }, 0, 48));
        Assert.Equal(32, NativeNormalFootObjectProcess.SelectSpeed(unpaused with { Speed = 0 }, 0, 48));
        NativeWorldRailAdvance riding = NativeWorldScriptedPathState.AdvanceRail(definition, initial.Nodes, 1, 1, 0, 100, 2);
        Assert.Equal(20, riding.Speed);
        Assert.Equal(0, riding.Pause);
        Assert.Equal(initial.Nodes.Select(node => node.Distance + 20), riding.Nodes.Select(node => node.Distance));
        NativeWorldRailAdvance ordinary = NativeWorldScriptedPathState.AdvanceRail(definition, initial.Nodes, 1, 1, 0, 100, 2, selectedSpeed: 32);
        Assert.Equal(32, ordinary.Speed);
        Assert.Equal(initial.Nodes.Select(node => node.Distance + 32), ordinary.Nodes.Select(node => node.Distance));
        NativeWorldRailAdvance stopped = NativeWorldScriptedPathState.AdvanceRail(definition, ordinary.Nodes, 1, 0, ordinary.Speed, 100, 2, selectedSpeed: 0);
        Assert.Equal(0, stopped.Speed);
        Assert.Equal(ordinary.Nodes.Select(node => node.Distance), stopped.Nodes.Select(node => node.Distance));
    }

    [Fact]
    public void FreshStory315FootLoaderRendersAndContinuesItsOwnPhysicalObjectsAfterIndependentRestore()
    {
        GameSession session = ComponentSession();
        using WorldMapScene world = new(session, TestResources.GameDirectory);
        Assert.Equal(new WorldTransition(5, 0, -1), session.State.WorldArrival);
        NativeFootWorldRuntimeState foot = world.State.NativeFootRuntime!;
        Assert.Null(foot.StationTrains);
        Assert.Null(foot.TrainDismount);
        Assert.Equal(9, foot.NormalObjects!.Allocation.Objects.Length);
        Assert.Equal(uint.MaxValue, foot.NormalObjects.PrimaryDistanceSquared);
        Assert.Equal(320 * 224 * 4, world.Render(320, 224).Length);
        GameState independentState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession independentSession = new(independentState);
        using WorldMapScene independent = new(independentSession, TestResources.GameDirectory);
        Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), independent.Apply(wait));
        Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(independentSession.State.World));
        Assert.Equal(1, world.State.NativeFootRuntime!.NormalObjects!.Callback);
        Assert.Equal(29, world.State.NativeFootRuntime.NormalObjects.Primary!.Pause);
        Assert.All(world.State.NativeFootRuntime.NormalObjects.Primary!.Presentation, pose => Assert.NotNull(pose.Projection));
        Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
    }

    [Theory]
    [InlineData(315, 9)]
    [InlineData(345, 13)]
    [InlineData(352, 1)]
    public void OriginalDisabledRailSelectorsRetainStaticAllocationWithoutProducingBankOrMinimumWords(ushort story, int count)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(story, 1);
        var camera = new NativeWorldFootCameraState(-32742, -26874, -755, -32742, -26874, -1011, 3760, -341, -341, -7168);
        var process = new NativeNormalFootObjectProcess(1, allocation, 0, null, null, camera, null, null);
        process.Validate();
        Assert.Equal(count, process.Allocation.Objects.Length);
        Assert.Equal(0, process.Allocation.ControlledObjectIndex);
        NativeNormalFootObjectProcess restored = JsonSerializer.Deserialize<NativeNormalFootObjectProcess>(JsonSerializer.Serialize(process))!;
        restored.Validate();
        Assert.Null(restored.Primary);
        Assert.Null(restored.Alternate);
        Assert.Null(restored.PrimaryDistanceSquared);
        Assert.Null(restored.AlternateDistanceSquared);
        Assert.Throws<InvalidDataException>(() => (process with { PrimaryDistanceSquared = uint.MaxValue }).Validate());
        Assert.Throws<InvalidDataException>(() => (process with { AlternateDistanceSquared = uint.MaxValue }).Validate());
        Assert.Throws<InvalidDataException>(() => (process with { Primary = NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1), PrimaryDistanceSquared = uint.MaxValue }).Validate());
        Assert.Throws<InvalidDataException>(() => (process with { Allocation = assets.ReadTrainWorldAllocation(story, 0) }).Validate());
    }

    [Fact]
    public void DisabledRailFootLoaderRendersStaticObjectsAndContinuesAfterIndependentRestore()
    {
        GameSession session = ComponentSession(1);
        using WorldMapScene world = new(session, TestResources.GameDirectory);
        AssertAbsentRails(world.State);
        Assert.Equal(9, world.State.NativeFootRuntime!.NormalObjects!.Allocation.Objects.Length);
        byte[] pixels = world.Render(320, 224);
        Assert.Equal(320 * 224 * 4, pixels.Length);
        GameState independentState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession independentSession = new(independentState);
        using WorldMapScene independent = new(independentSession, TestResources.GameDirectory);
        Assert.Equal(pixels, independent.Render(320, 224));
        foreach (PlayerAction action in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: 0) })
        {
            Assert.Equal(world.Apply(action), independent.Apply(action));
            AssertAbsentRails(world.State);
            Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(independentSession.State.World));
            Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
        }
        Assert.Equal(2, world.State.NativeFootRuntime!.NormalObjects!.Callback);

        static void AssertAbsentRails(WorldState state)
        {
            NativeNormalFootObjectProcess process = state.NativeFootRuntime!.NormalObjects!;
            process.Validate();
            Assert.Null(process.Primary);
            Assert.Null(process.Alternate);
            Assert.Null(process.PrimaryDistanceSquared);
            Assert.Null(process.AlternateDistanceSquared);
            Assert.Null(state.NativeTrainRuntime);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DelingSingleFootAllocationPreservesBoardingBitAndActualRailSelectorsAfterRestore(byte eventFlags265)
    {
        GameSession session = ComponentSession(eventFlags265, story: 352, replacementFlags: 8, spawn: 8);
        using WorldMapScene world = new(session, TestResources.GameDirectory);
        NativeNormalFootObjectProcess process = world.State.NativeFootRuntime!.NormalObjects!;
        Assert.Equal(5, process.Allocation.ClauseIndex);
        Assert.Equal((ushort)348, process.Allocation.MinimumStory);
        Assert.Equal((ushort)392, process.Allocation.MaximumStory);
        Assert.Single(process.Allocation.Objects);
        Assert.Equal(0, process.Allocation.ControlledObjectIndex);
        Assert.Equal((ushort)0, process.Allocation.Objects[0].RawOperand);
        Assert.Equal((byte)8, session.WorldEntryMemory!.ReplacementFlags264);
        Assert.Equal(eventFlags265 == 1, process.Primary is null);
        Assert.Equal(eventFlags265 == 1, process.Alternate is null);
        GameSession independentSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene independent = new(independentSession, TestResources.GameDirectory);
        foreach (PlayerAction action in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: 0) })
        {
            Assert.Equal(world.Apply(action), independent.Apply(action));
            Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(independentSession.State.World));
            Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
        }
        Assert.Equal(2, world.State.NativeFootRuntime.NormalObjects!.Callback);
        Assert.Equal(eventFlags265 == 1, world.State.NativeFootRuntime.NormalObjects.PrimaryDistanceSquared is null);
        Assert.Equal(eventFlags265 == 1, world.State.NativeFootRuntime.NormalObjects.AlternateDistanceSquared is null);
    }

    [Fact]
    public void DelingOrdinaryFootStillRefusesUnimplementedAuxiliaryPairsAndFollowingClause()
    {
        GameSession pair = ComponentSession(story: 352, replacementFlags: 9, spawn: 8);
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(pair, TestResources.GameDirectory));
        GameSession following = ComponentSession(story: 392, replacementFlags: 8, spawn: 8);
        Assert.Throws<NotSupportedException>(() => new WorldMapScene(following, TestResources.GameDirectory));
    }

    [Fact]
    public void NormalTrainEndpointRetainsContactMinimumAndDrawProjectionThroughItsPhysicalReset()
    {
        GameSession session = ComponentSession(story: 352, replacementFlags: 8, spawn: 8);
        using WorldMapScene world = new(session, TestResources.GameDirectory);
        PlayerAction wait = new(PlayerActionKind.Wait);
        for (int callback = 0; callback < 2000 && world.State.NativeFootRuntime!.NormalObjects!.Alternate!.EndpointProjection is null; callback++)
        {
            Assert.True(world.Apply(wait).Accepted);
        }
        NativeNormalFootObjectProcess process = world.State.NativeFootRuntime!.NormalObjects!;
        NativeTrainRailBank endpoint = process.Alternate!;
        NativeTrainRailEndpointProjection receipt = Assert.IsType<NativeTrainRailEndpointProjection>(endpoint.EndpointProjection);
        Assert.Equal(world.State.Frame, receipt.Callback);
        Assert.NotEqual(endpoint.Nodes[0].Position, endpoint.Presentation[0].Projection!.Source);
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(endpoint.TrackIndex);
        endpoint.Validate(definition);
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        NativeWorldPathWords player = context.RelativeWords(new NativeWorldPathPosition(world.State.X, world.State.NativeHeight, -world.State.Z));
        uint retainedMinimum = Minimum(false);
        Assert.Equal(retainedMinimum, process.AlternateDistanceSquared);
        Assert.NotEqual(retainedMinimum, Minimum(true));
        byte[] pixels = world.Render(320, 224);
        GameSession independentSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene independent = new(independentSession, TestResources.GameDirectory);
        Assert.Equal(pixels, independent.Render(320, 224));
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1, Vertical: 0);
        Assert.Equal(world.Apply(move), independent.Apply(move));
        Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(independentSession.State.World));
        Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
        NativeTrainRailBank following = world.State.NativeFootRuntime!.NormalObjects!.Alternate!;
        Assert.Null(following.EndpointProjection);
        Assert.Equal(following.Nodes.Select(node => node.Position), following.Presentation.Select(pose => pose.Projection!.Source));

        uint Minimum(bool useResetPositions)
        {
            uint minimum = uint.MaxValue;
            for (int index = 0; index < endpoint.Presentation.Length; index++)
            {
                NativeWorldPathNodeProjection projection = endpoint.Presentation[index].Projection!;
                if (!context.IsNear(useResetPositions ? endpoint.Nodes[index].Position : projection.Source))
                {
                    continue;
                }
                NativeWorldPathWords words = useResetPositions ? context.RelativeWords(endpoint.Nodes[index].Position) : projection.Words;
                short dx = unchecked((short)(player.X - words.X));
                short dy = unchecked((short)(player.Y - words.Y));
                short dz = unchecked((short)(player.Z - words.Z));
                minimum = Math.Min(minimum, unchecked((uint)(dx * dx + dy * dy + dz * dz)));
            }
            return minimum;
        }
    }

    private static GameSession ComponentSession(byte eventFlags265 = 0, ushort story = 315, byte replacementFlags = 0, byte spawn = 5)
    {
        GameSession initialized = GameSession.CreateColdProcess();
        initialized.StartNewGame();
        using GameAssets assets = new(TestResources.GameDirectory);
        initialized.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        initialized.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        initialized.SetWorldEntryMemory(initialized.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(264, 1, replacementFlags).WriteBytes(265, 1, eventFlags265));
        WorldTransition packet = new(spawn, 0, -1);
        return new GameSession(initialized.State with { Scene = SceneKind.WorldMap, FieldId = 822, WorldArrival = packet, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(packet, 822) });
    }
}
