using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeParkedWorldBattleReloadTests
{
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
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(reloadedSession.State))!);
        using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
        Assert.Equal(reloaded.Render(320, 224), restored.Render(320, 224));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(reloaded.Apply(next), restored.Apply(next));
        Assert.Equal(JsonSerializer.Serialize(reloaded.State), JsonSerializer.Serialize(restored.State));
    }
}
