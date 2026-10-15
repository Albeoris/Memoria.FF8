using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Declared native components using installed original resources; these fixtures do not represent an authentic player journal.</summary>
public sealed class NativeNormalFootObjectTests
{
    [Theory]
    [InlineData(290, 3, 9)]
    [InlineData(315, 3, 9)]
    [InlineData(344, 3, 9)]
    [InlineData(345, 4, 13)]
    [InlineData(347, 4, 13)]
    public void OriginalSelectedClauseRetainsEveryDenseObjectAndBothFreshRailBanks(ushort story, int clause, int count)
    {
        using WorldAssets assets = new(LaunchOptions.DefaultGameDirectory);
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
        Assert.Throws<NotSupportedException>(() => (process with { Allocation = assets.ReadTrainWorldAllocation(story, 1), Alternate = null, AlternateDistanceSquared = null }).Validate());
        Assert.Throws<NotSupportedException>(() => assets.ReadFootAllocation(0, story));
    }

    [Fact]
    public void PriorProximityWordSelectsNativeThirtyTwoZeroCycleWithoutChangingModeThreeDefault()
    {
        using WorldAssets assets = new(LaunchOptions.DefaultGameDirectory);
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
        using WorldMapScene world = new(session, LaunchOptions.DefaultGameDirectory);
        Assert.Equal(new WorldTransition(5, 0, -1), session.State.WorldArrival);
        NativeFootWorldRuntimeState foot = world.State.NativeFootRuntime!;
        Assert.Null(foot.StationTrains);
        Assert.Null(foot.TrainDismount);
        Assert.Equal(9, foot.NormalObjects!.Allocation.Objects.Length);
        Assert.Equal(uint.MaxValue, foot.NormalObjects.PrimaryDistanceSquared);
        Assert.Equal(320 * 224 * 4, world.Render(320, 224).Length);
        GameState independentState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession independentSession = new(independentState);
        using WorldMapScene independent = new(independentSession, LaunchOptions.DefaultGameDirectory);
        Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), independent.Apply(wait));
        Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(independentSession.State.World));
        Assert.Equal(1, world.State.NativeFootRuntime!.NormalObjects!.Callback);
        Assert.Equal(29, world.State.NativeFootRuntime.NormalObjects.Primary.Pause);
        Assert.All(world.State.NativeFootRuntime.NormalObjects.Primary.Presentation, pose => Assert.NotNull(pose.Projection));
        Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
    }

    private static GameSession ComponentSession()
    {
        GameSession initialized = GameSession.CreateColdProcess();
        initialized.StartNewGame();
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        initialized.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        initialized.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        WorldTransition packet = new(5, 0, -1);
        return new GameSession(initialized.State with { Scene = SceneKind.WorldMap, FieldId = 822, WorldArrival = packet, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(packet, 822), Variables = new Dictionary<int, int> { [256] = 315 }, ReplayJournal = null });
    }
}
