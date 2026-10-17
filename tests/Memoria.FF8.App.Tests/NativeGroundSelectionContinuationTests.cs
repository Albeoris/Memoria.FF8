using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGroundSelectionContinuationTests
{
    [Fact]
    public void UniformFootPassesTheCoastBoundaryWithColdSelectionAndRestoresTheNextCallback()
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 29));
        Assert.Equal((29L, 13381, -26647, -312), (scene.State.Frame, scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Single(scene.State.NativeFootRuntime!.GroundCache!.Alternatives);
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
        PlayerAction next = new(PlayerActionKind.Move, Vertical: -1, Frames: 6);
        Assert.Equal(scene.Apply(next), restored.Apply(next));
        Assert.Equal(35, scene.State.Frame);
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restoredSession.State));
        Assert.Equal(scene.Render(320, 224), restored.Render(320, 224));
    }

    [Fact]
    public void HistoricalUnknownSelectionStillRefusesAtTheSameCoastBoundaryWithoutMutation()
    {
        GameSession session = UniformEntry();
        session.SetWorldProcess(session.WorldProcess! with { GroundSelection = null });
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1, Frames: 29));
        string before = JsonSerializer.Serialize(session.State);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => scene.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: -1)));
        Assert.Equal("Retained native world cache/input alternatives disagree on the physical ground result.", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Theory]
    [InlineData(0, -1, 60)]
    [InlineData(0, 1, 60)]
    [InlineData(-1, 0, 60)]
    [InlineData(1, 0, 60)]
    [InlineData(-1, -1, 60)]
    [InlineData(-1, 1, 60)]
    [InlineData(1, -1, 60)]
    [InlineData(1, 1, 33)]
    public void DigitalDirectionsKeepOneExecutedSelectionAcrossTheEntryHold(int horizontal, int vertical, int frames)
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal, Vertical: vertical, Frames: frames));
        Assert.Equal(frames, scene.State.Frame);
        NativeWorldGroundAlternative actual = Assert.Single(scene.State.NativeFootRuntime!.GroundCache!.Alternatives);
        Assert.Equal(actual.PreferredGroup, Assert.Single(session.WorldProcess!.GroundSelection!.Alternatives).PreferredGroup);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    public void FootHorizontalEdgesUseTheOriginalMappedButtonGroup(int horizontal, int expected)
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 24));
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal));
        Assert.Equal(expected, Assert.Single(scene.State.NativeFootRuntime!.GroundCache!.Alternatives).PreferredGroup);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: horizontal));
        Assert.Equal(expected, Assert.Single(scene.State.NativeFootRuntime!.GroundCache!.Alternatives).PreferredGroup);
    }

    [Fact]
    public void EntryHoldSuppressesVelocityButStillPublishesActualHorizontalEdges()
    {
        GameSession session = UniformEntry();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        int x = scene.State.X;
        int z = scene.State.Z;
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        Assert.Equal((x, z), (scene.State.X, scene.State.Z));
        Assert.Equal(-1, scene.State.NativeFootRuntime!.PreviousHorizontalInput);
        Assert.Equal(1, Assert.Single(scene.State.NativeFootRuntime.GroundCache!.Alternatives).PreferredGroup);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Assert.Equal((x, z), (scene.State.X, scene.State.Z));
        Assert.Equal(1, scene.State.NativeFootRuntime.PreviousHorizontalInput);
        Assert.Equal(0, Assert.Single(scene.State.NativeFootRuntime.GroundCache!.Alternatives).PreferredGroup);
    }

    [Fact]
    public void ReloadUsesLastMappedInputWithoutManufacturingAHorizontalEdge()
    {
        GameSession session = UniformEntry();
        // Declared retained metadata: a prior terrain fallback selected group1
        // while the right button remained held. This is not a traveled journey.
        NativeWorldProcess process = session.WorldProcess!.AdvanceMainLoopCallbacks(2) with
        {
            GroundSelection = new([new(1, 0)]),
            InputHistory = NativeWorldInputHistory.Sample(NativeWorldInputHistory.Sample(session.WorldProcess.InputHistory, 0x8000), 0x8000)
        };
        session.SetWorldProcess(process);
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.Equal(1, scene.State.NativeFootRuntime!.PreviousHorizontalInput);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Assert.Equal(1, Assert.Single(scene.State.NativeFootRuntime.GroundCache!.Alternatives).PreferredGroup);
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: 1);
        Assert.Equal(scene.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restoredSession.State));
    }

    [Fact]
    public void ReloadResetsTheBufferIndexAndUsesSlotZeroInsteadOfTheLastChronologicalInput()
    {
        GameSession session = UniformEntry();
        NativeWorldProcess process = session.WorldProcess!.AdvanceMainLoopCallbacks(1) with
        {
            GroundSelection = new([new(1, 0)]),
            InputHistory = NativeWorldInputHistory.Sample(session.WorldProcess.InputHistory, 0x8000)
        };
        session.SetWorldProcess(process);
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.Equal(0, scene.State.NativeFootRuntime!.PreviousHorizontalInput);
        scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1));
        Assert.Equal(0, Assert.Single(scene.State.NativeFootRuntime.GroundCache!.Alternatives).PreferredGroup);
        Assert.Equal((ushort)0, session.WorldProcess!.InputHistory!.PreviousButtons);
    }

    private static GameSession UniformEntry()
    {
        // Structural loader fixture using original initialization and spawn1;
        // this does not represent a completed field journey.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets resources = new(TestResources.GameDirectory);
        session.InitializeNewGameCharacterVitals(resources.ReadNewGameCharacterVitals());
        session.Restore(session.State with
        {
            Scene = SceneKind.WorldMap,
            WorldArrival = new WorldTransition(1, 1, -1),
            WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 135),
            SavedWorld = session.State.SavedWorld! with { Slot4 = new NativeSavedWorldVehicleSlot(255, new NativeSavedWorldPose(12884, -26807, -304, 2699)) }
        });
        return session;
    }
}
