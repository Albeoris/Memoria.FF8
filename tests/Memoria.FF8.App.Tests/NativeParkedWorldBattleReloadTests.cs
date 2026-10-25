using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeParkedWorldBattleReloadTests
{
    [Fact]
    public void ReachedStationFootReloadRetainsItsTrainPacketAcrossCallbacksAndSaveLoad()
    {
        GameSession source = new(GameSession.Load(TestResources.FixturePath("App.Tests.NativeParkedWorldBattleReloadTests.Foot")));
        WorldState departure = source.State.World!;
        Assert.NotNull(departure.NativeFootRuntime!.StationTrains);
        Assert.NotEqual(departure.NativeFootRuntime.EntityType, unchecked((byte)source.State.WorldArrival!.Mode));
        using Memoria.FF8.Assets.WorldAssets locations = new(TestResources.GameDirectory);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordBattleDeparture(departure, checked((ushort)source.State.FieldId), WorldEntryVariables.Read(source.State).StoryWord, locations.GetNativeMapLocationId(departure.X, departure.Z));
        GameSession reloadedSession = new(source.State with { World = null, DepartingWorldState = departure, SavedWorld = source.State.SavedWorld!.WithFootReturn(receipt) });
        using WorldMapScene reloaded = new(reloadedSession, TestResources.GameDirectory, battleReload: receipt);
        Assert.Equal(source.State.WorldArrival, reloadedSession.State.WorldArrival);
        Assert.True(reloaded.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Throws<InvalidDataException>(() => new GameSession(reloadedSession.State with { SavedWorld = reloadedSession.State.SavedWorld! with { FootReturn = null } }));
        foreach (NativeSavedWorldFootReturn forged in new[]
        {
            receipt with { Rule = NativeSavedWorldFootReturn.SaveRule, WorldMode = 0 },
            receipt with { SourceWorldFrame = receipt.SourceWorldFrame + 1 },
            receipt with { EntityType105 = 1 },
            receipt with { AnimationVariant108 = (byte)(1 - receipt.AnimationVariant108!.Value) }
        })
        {
            Assert.Throws<InvalidDataException>(() => new GameSession(reloadedSession.State with { SavedWorld = reloadedSession.State.SavedWorld! with { FootReturn = forged } }));
        }
        string path = Path.Combine(Path.GetTempPath(), "ff8-station-foot-reload-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            reloadedSession.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
            Assert.Equal(source.State.WorldArrival, restoredSession.State.WorldArrival);
            PlayerAction next = new(PlayerActionKind.Wait);
            Assert.Equal(reloaded.Apply(next), restored.Apply(next));
            Assert.Equal(JsonSerializer.Serialize(reloaded.State), JsonSerializer.Serialize(restored.State));
            WorldState nextDeparture = reloaded.State;
            NativeSavedWorldFootReturn nextReceipt = NativeSavedWorldFootReturn.RecordBattleDeparture(nextDeparture, checked((ushort)reloadedSession.State.FieldId), WorldEntryVariables.Read(reloadedSession.State).StoryWord, locations.GetNativeMapLocationId(nextDeparture.X, nextDeparture.Z));
            Assert.NotEqual(nextReceipt.SourceWorldFrame, nextDeparture.NativeFootRuntime!.ReloadSourceWorldFrame);
            // Validation fixture for the next departure's saved bytes while the current exploration allocation remains live.
            GameSession nextDepartureSession = new(reloadedSession.State with { SavedWorld = reloadedSession.State.SavedWorld!.WithFootReturn(nextReceipt), WorldConfiguration = NativeWorldConfiguration.FromWorldReturn(nextDeparture, reloadedSession.State.FieldId) });
            nextDepartureSession.Save(path);
            GameState savedDeparture = GameSession.Load(path);
            Assert.Equal(nextReceipt, savedDeparture.SavedWorld!.FootReturn);
            Assert.Equal(nextDeparture.NativeFootRuntime.ReloadSourceWorldFrame, savedDeparture.World!.NativeFootRuntime!.ReloadSourceWorldFrame);
            Assert.Equal(source.State.WorldArrival, savedDeparture.WorldArrival);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Garden")]
    [InlineData("Ragnarok")]
    [InlineData("Station")]
    public void ActualMode3DepartureReloadsOriginalParkedAllocationAndRestoresTheNextCallback(string branch)
    {
        string fixture = branch switch
        {
            "Garden" => "App.Tests.ParkedGardenInfReturnTests.1",
            "Ragnarok" => "App.Tests.ParkedRagnarokInfReturnTests.1",
            "Station" => "App.Tests.NativeTrainStationChoiceTests.1",
            _ => throw new ArgumentOutOfRangeException(nameof(branch))
        };
        GameSession source = new(GameSession.Load(TestResources.FixturePath(fixture)));
        using GameWorld current = new(source, TestResources.GameDirectory, validateWorldPresentation: false);
        if (branch == "Station")
        {
            current.EnsureField();
            Assert.True(current.Apply(new(PlayerActionKind.Choose, Value: 1)).Accepted);
        }
        else
        {
            PlayerAction exit = branch == "Garden" ? new(PlayerActionKind.Move, Horizontal: 0.131f, Vertical: 0.99138f, Frames: 4) : new(PlayerActionKind.Move, Horizontal: 0.02942f, Vertical: 0.99957f, Frames: 6);
            Assert.True(current.Apply(exit).Accepted);
        }
        WorldState departure = source.State.World!;
        using Memoria.FF8.Assets.WorldAssets locations = new(TestResources.GameDirectory);
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordBattleDeparture(departure, checked((ushort)source.State.FieldId), WorldEntryVariables.Read(source.State).StoryWord, locations.GetNativeMapLocationId(departure.X, departure.Z));
        NativeSavedWorldState saved = source.State.SavedWorld!;
        if (departure.NativeGardenRuntime is { } garden)
        {
            saved = saved.WithGardenParking(garden);
        }
        if (departure.NativeRagnarokRuntime is { } ship)
        {
            saved = saved.WithRagnarokParking(ship);
        }
        GameSession reloadedSession = new(source.State with { World = null, DepartingWorldState = departure, SavedWorld = saved.WithFootReturn(receipt) });
        using WorldMapScene reloaded = new(reloadedSession, TestResources.GameDirectory, battleReload: receipt);
        Assert.Equal(source.State.WorldArrival, reloadedSession.State.WorldArrival);
        Assert.Equal(NativeFootWorldRuntimeState.BattleReloadRule, reloaded.State.NativeFootRuntime!.Rule);
        Assert.Equal((departure.X, departure.Z, departure.BodyYaw, departure.CameraYaw), (reloaded.State.X, reloaded.State.Z, reloaded.State.BodyYaw, reloaded.State.CameraYaw));
        if (branch == "Garden")
        {
            Assert.NotNull(reloaded.State.NativeGardenRuntime);
            Assert.Equal(saved.GardenPose3, reloaded.State.NativeGardenRuntime.Allocation.GardenPose);
        }
        else if (branch == "Ragnarok")
        {
            Assert.NotNull(reloaded.State.NativeRagnarokRuntime);
            Assert.Equal(saved.RagnarokPose2, reloaded.State.NativeRagnarokRuntime.Allocation.ParkedRagnarokPose2);
        }
        else
        {
            Assert.NotNull(reloaded.State.NativeFootRuntime.NormalObjects);
            Assert.Equal(0, reloaded.State.NativeFootRuntime.NormalObjects.Callback);
        }
        Assert.Throws<InvalidDataException>(() => new GameSession(reloadedSession.State with { SavedWorld = reloadedSession.State.SavedWorld! with { FootReturn = null } }));
        foreach (NativeSavedWorldFootReturn forged in new[]
        {
            receipt with { Rule = NativeSavedWorldFootReturn.SaveRule, WorldMode = 0 },
            receipt with { SourceWorldFrame = receipt.SourceWorldFrame + 1 },
            receipt with { EntityType105 = 1 }
        })
        {
            Assert.Throws<InvalidDataException>(() => new GameSession(reloadedSession.State with { SavedWorld = reloadedSession.State.SavedWorld! with { FootReturn = forged } }));
        }
        string path = Path.Combine(Path.GetTempPath(), "ff8-world-battle-reload-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            reloadedSession.Save(path);
            GameState savedReload = GameSession.Load(path);
            Assert.Equal(reloadedSession.State.WorldArrival, savedReload.WorldArrival);
            Assert.Equal(receipt, savedReload.SavedWorld!.FootReturn);
            Assert.Equal(JsonSerializer.Serialize(reloadedSession.State.World), JsonSerializer.Serialize(savedReload.World));
        }
        finally
        {
            File.Delete(path);
        }
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(reloadedSession.State))!);
        using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
        Assert.Equal(reloaded.Render(320, 224), restored.Render(320, 224));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(reloaded.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(reloaded.State), JsonSerializer.Serialize(restored.State));
    }
}
