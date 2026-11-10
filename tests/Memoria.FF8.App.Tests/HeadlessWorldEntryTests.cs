using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HeadlessWorldEntryTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(61, 32, 455, 8, 5)]
    [InlineData(3, 35, 300, 8, 0)]
    [InlineData(12, 48, 750, 9, 2)]
    [InlineData(32, 50, 3500, 0, 4)]
    public void FirstVehicleControlsPromptClosesWithoutStartingATransfer(short entry, byte entity, ushort story, byte replacements, ushort flag)
    {
        GameSession source = GameSession.CreateColdProcess();
        source.StartNewGame();
        using GameWorld initialization = new(source, GameDirectory);
        GameState departure = source.State with { WorldEntryMemory = source.State.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(264, 1, replacements).WriteBytes(265, 1, 1) };
        WorldTransition packet = new(entry, entity, -1);
        using PreparedWorldEntry prepared = WorldEntryPreflight.Prepare(departure, GameDirectory, packet, validatePresentation: false);
        GameSession committed = new(departure);
        committed.CommitPreparedWorldEntry(packet, prepared.InitialState, departure.WorldControls!, prepared.ValidateAdoption, prepared.InitialSavedWorld, prepared.InitialEventMemory);
        using WorldMapScene scene = prepared.Adopt(committed);

        scene.Apply(new(PlayerActionKind.Wait, Frames: 60));
        Assert.Equal(14, scene.State.EventUi!.ScriptMode);
        Assert.Contains(scene.State.EventUi.Windows, window => window.WindowId is 9 or 11);
        Assert.True(committed.State.SavedWorld!.WorldEventFlags116!.Matches((ushort)(0x0100 | flag)));
        if (flag == 5)
        {
            // The Missile Base briefing and the car controls are separate native events.
            Assert.Equal(11, scene.CurrentDialogue?.MessageId);
            for (int callback = 0; callback < 180 && scene.State.EventUi!.MatchesTextComplete(11, 1) != true; callback++)
            {
                scene.Apply(new(PlayerActionKind.Wait));
            }
            Assert.True(scene.State.EventUi!.MatchesTextComplete(11, 1));
            Assert.Equal(PlayerTriggerKind.WorldMap, scene.Apply(new(PlayerActionKind.Confirm)).Trigger.Kind);
            Assert.True(committed.State.SavedWorld!.WorldEventFlags116!.Matches(0x003D));
            Assert.Equal(49, scene.Apply(new(PlayerActionKind.Wait)).Trigger.MessageId);
        }
        Assert.Equal(1, scene.Apply(new(PlayerActionKind.Confirm)).FramesAdvanced);
        Assert.Equal(0, scene.State.EventUi!.ScriptMode);
        Assert.Null(scene.State.NativeGardenRuntime?.Transfer);
        Assert.Null(scene.State.NativeRagnarokRuntime?.Transfer);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.True(committed.State.SavedWorld!.WorldEventFlags116!.Matches(0x003F));
        Assert.True(committed.State.SavedWorld.WorldEventFlags116.Matches((ushort)(0x0100 | flag)));
        for (int callback = 0; callback < 4; callback++)
        {
            Assert.DoesNotContain(scene.State.EventUi.Windows, window => window.WindowId is 9 or 11);
            Assert.Equal(1, scene.Apply(new(PlayerActionKind.Wait)).FramesAdvanced);
        }
    }

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
