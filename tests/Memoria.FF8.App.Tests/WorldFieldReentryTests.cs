using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldFieldReentryTests
{
    [Fact]
    public void ActualNaturalFireCavernEntranceRestoresItsNativePacketFootReceiptAndFieldVm()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world/field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string prefix = Path.Combine(repository!.FullName, "../fixtures");
        var session = new GameSession(GameSession.Load(prefix + "entry-preboundary-checkpoint.json"));
        var replaySession = new GameSession(GameSession.Load(prefix + "entry-preboundary-checkpoint.json"));
        using var world = new GameWorld(session, directory);
        using var replay = new GameWorld(replaySession, directory);
        world.EnsureField();
        replay.EnsureField();
        JsonElement frozenVm = world.Events.Capture();
        Assert.Equal(1649, session.State.World!.Frame);
        PlayerAction next = new(PlayerActionKind.Move, Horizontal: -1, Vertical: -1);
        PlayerActionResult entered = world.Apply(next);
        Assert.Equal(entered, replay.Apply(next));
        Assert.Equal(PlayerTriggerKind.FieldTransition, entered.Trigger.Kind);
        Assert.Equal((136, 1650L), (entered.Trigger.FieldId, entered.Trigger.Frame));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        GameState expected = GameSession.Load(prefix + "arrival-checkpoint.json");
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(session.State)));
        Assert.Equal(new FieldTransition(136, 175, -2269, 12, 128), session.State.FieldArrival);
        Assert.Equal(159, session.State.PreviousFieldId);
        Assert.Null(session.State.EventState);
        Assert.True(JsonElement.DeepEquals(frozenVm, session.State.DepartingFieldState!.Value));
        Assert.Equal(1650, session.State.DepartingWorldState!.Frame);
        Assert.Equal(new NativeSavedWorldPose(30181, -29222, -693, 165), session.State.SavedWorld!.FootReturn!.Pose0);
        Assert.Equal(1, session.State.SavedWorld.FootReturn.MapLocationId);
        Assert.Null(session.State.SavedWorld.FootReturn.AnimationVariant108);
        Assert.Null(session.State.SavedWorld.FootReturn.MapDisplayMode110);
        Assert.True(world.EnsureField());
        Assert.True(replay.EnsureField());
        Assert.Equal(136, world.Events.FieldId);
        Assert.Equal((ushort?)159, world.Events.Scene.PreviousFieldWord84);
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), replay.Apply(new PlayerAction(PlayerActionKind.Wait)));
        GameState frame1 = GameSession.Load(prefix + "frame1-checkpoint.json");
        Assert.True(JsonElement.DeepEquals(frame1.EventState!.Value, world.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
        world.SetRenderSize(320, 240);
        replay.SetRenderSize(320, 240);
        Assert.Equal(world.Render(TimeSpan.Zero), replay.Render(TimeSpan.Zero));
    }
}
