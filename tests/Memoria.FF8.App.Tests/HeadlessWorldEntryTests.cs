using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HeadlessWorldEntryTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void HeadlessPreparationRetainsAllSimulationOwnersAndSelectorConsumptionWhileLiveRasterFailsExplicitly()
    {
        GameSession source = GameSession.CreateColdProcess();
        source.StartNewGame();
        using GameWorld initialization = new(source, GameDirectory);
        GameState departure = source.State with
        {
            WorldEntryMemory = source.State.WorldEntryMemory!.WriteBytes(256, 2, 3500),
            EventMemory = source.State.EventMemory.Write(269, 1, NativeScalarValue.Exact(4)),
            SavedWorld = source.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(260) }
        };
        string before = JsonSerializer.Serialize(departure);
        WorldTransition packet = new(32, 50, -1);
        NotSupportedException refusal = Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, GameDirectory, packet));
        Assert.Contains("auxiliary actor", refusal.Message);
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(departure, GameDirectory, packet, validatePresentation: false);
        Assert.NotNull(prepared.InitialState.NativeRagnarokRuntime!.GroundCache);
        Assert.NotNull(prepared.InitialState.NativeRagnarokRuntime.Cactuar);
        Assert.NotNull(prepared.InitialState.NativeRagnarokRuntime.Forest);
        Assert.Equal(0, prepared.InitialEventMemory.Read(269, 1).RequireExact());
        Assert.Equal(4, departure.EventMemory.Read(269, 1).RequireExact());
        GameSession committed = new(departure);
        committed.CommitPreparedWorldEntry(packet, prepared.InitialState, departure.WorldControls!, prepared.ValidateAdoption, prepared.InitialSavedWorld, prepared.InitialEventMemory);
        using WorldMapScene scene = prepared.Adopt(committed);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(1, scene.State.Frame);
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(committed.State))!);
        using WorldMapScene restored = new(restoredSession, GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(JsonSerializer.Serialize(committed.State), JsonSerializer.Serialize(restoredSession.State));
        Assert.Equal(before, JsonSerializer.Serialize(departure));
        Assert.Throws<NotSupportedException>(() => scene.Render(320, 224));
    }
}
