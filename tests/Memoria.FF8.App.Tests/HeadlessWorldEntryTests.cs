using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HeadlessWorldEntryTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void ColdFieldToGardenEntryContinuesWithoutADepartingWorld()
    {
        GameSession source = GameSession.CreateColdProcess();
        source.StartNewGame();
        using GameWorld initialization = new(source, GameDirectory);
        GameState departure = source.State with { WorldEntryMemory = source.State.WorldEntryMemory!.WriteBytes(256, 2, 750).WriteBytes(264, 1, 9) };
        Assert.Null(departure.DepartingWorldState);
        WorldTransition packet = new(12, 48, -1);
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(departure, GameDirectory, packet);
        Assert.False(prepared.InitialState.NativeGardenRuntime!.PriorTerrainOcclusion);
        GameSession committed = new(departure);
        committed.CommitPreparedWorldEntry(packet, prepared.InitialState, departure.WorldControls!, prepared.ValidateAdoption, prepared.InitialSavedWorld, prepared.InitialEventMemory);
        using WorldMapScene scene = prepared.Adopt(committed);
        Assert.Equal(24, scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Move, Vertical: -1)).FramesAdvanced);
    }

    [Fact]
    public void ColdFootEntryWithParkedRagnarokUsesNativeInitialProjectionAndContinues()
    {
        GameSession source = NativeRagnarokFootTests.Fixture();
        using (WorldMapScene flight = new(source, GameDirectory))
        {
            NativeRagnarokFootTests.Land(flight);
        }
        WorldState landed = source.State.World!;
        NativeSavedWorldState saved = source.State.SavedWorld!.WithRagnarokParking(landed.NativeRagnarokRuntime!);
        NativeSavedWorldFootReturn foot = NativeSavedWorldFootReturn.RecordFootDeparture(landed, (ushort)source.State.FieldId, 3500, 0);
        GameSession resumed = new(source.State with { World = null, DepartingWorldState = null, WorldArrival = new(62, 0, -1), WorldProcess = NativeWorldProcess.FromColdProcess(), SavedWorld = saved.WithFootReturn(foot) });
        using WorldMapScene scene = new(resumed, GameDirectory);
        NativeRagnarokWorldRuntimeState ship = scene.State.NativeRagnarokRuntime!;
        Assert.Equal(saved.RagnarokPose2, ship.Allocation.ParkedRagnarokPose2);
        Assert.Equal(foot.Pose0, ship.Allocation.LoadPose);
        Assert.False(ship.PriorTerrainOcclusion);
        using WorldAssets assets = new(GameDirectory);
        NativeRagnarokTransferCamera ordinary = assets.ReadRagnarokTransferCameraTargets(NativeRagnarokTransferKind.Landing, ship.CurrentGroundHeight!.Value);
        Assert.NotEqual(ordinary.ProjectionWords.ToArray(), ship.TransferCamera!.ProjectionWords.ToArray());
        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);
        Assert.Equal(ordinary.ProjectionWords.ToArray(), scene.State.NativeRagnarokRuntime!.TransferCamera!.ProjectionWords.ToArray());
        Assert.Equal(ordinary.PerspectiveWords.ToArray(), scene.State.NativeRagnarokRuntime.TransferCamera.PerspectiveWords.ToArray());
    }

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
