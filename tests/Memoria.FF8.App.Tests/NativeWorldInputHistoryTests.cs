using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldInputHistoryTests
{
    [Fact]
    public void ActualCarGuardConfirmUsesSharedSamplesAndRepeatsAfterSnapshot()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using WorldMapScene scene = new(session, directory);
        Assert.Equal(0, scene.State.Frame);
        Assert.Null(session.WorldProcess!.InputHistory);
        scene.DebugPlaceCurrentCar(12390, -26709);
        PlayerActionResult guard = scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
        Assert.Equal(PlayerTriggerKind.Dialogue, guard.Trigger.Kind);
        Assert.Equal(49, guard.Trigger.MessageId);
        Assert.Equal((byte)14, scene.State.EventUi!.ScriptMode);
        Assert.Equal(24, session.WorldProcess.InputHistory!.Samples);
        Assert.Equal((ushort)0, session.WorldProcess.InputHistory.CurrentButtons);
        Assert.Equal((ushort)0, session.WorldProcess.InputHistory.PreviousButtons);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld first = new(session, directory);
        using GameWorld replay = new(restored, directory);
        first.EnsureField();
        replay.EnsureField();
        PlayerAction confirm = new(PlayerActionKind.Confirm);
        PlayerActionResult actual = first.Apply(confirm);
        Assert.True(actual.Accepted);
        Assert.Equal(PlayerTriggerKind.FieldTransition, actual.Trigger.Kind);
        Assert.Equal(106, actual.Trigger.FieldId);
        Assert.Equal(actual, replay.Apply(confirm));
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(106, session.State.FieldId);
        Assert.Equal((byte)33, session.State.SavedWorld!.CarReturn!.EntityType105);
        Assert.Empty(session.State.DepartingWorldState!.EventUi!.Windows);
        Assert.Equal((byte)0, session.State.DepartingWorldState.EventUi.ScriptMode);
        Assert.Equal((ushort)0x40, session.WorldProcess!.InputHistory!.CurrentButtons);
        Assert.Equal((ushort)0, session.WorldProcess.InputHistory.PreviousButtons);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    [Fact]
    public void OldMidWorldHistoryIsEstablishedByARealReleaseCallbackBeforeConfirm()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using (WorldMapScene source = new(session, directory))
        {
            source.DebugPlaceCurrentCar(12390, -26709);
            Assert.Equal(49, source.Apply(new(PlayerActionKind.Wait, Frames: 24)).Trigger.MessageId);
        }
        JsonObject old = JsonNode.Parse(JsonSerializer.Serialize(session.State))!.AsObject();
        old["WorldProcess"]!.AsObject().Remove("InputHistory");
        GameSession legacy = new(JsonSerializer.Deserialize<GameState>(old.ToJsonString())!);
        using WorldMapScene scene = new(legacy, directory);
        Assert.Null(legacy.WorldProcess!.InputHistory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(25, scene.State.Frame);
        Assert.Equal((ushort)0, legacy.WorldProcess.InputHistory!.CurrentButtons);
        Assert.Null(legacy.WorldProcess.InputHistory.PreviousButtons);
        Assert.Equal(49, scene.CurrentDialogue!.MessageId);
        using GameWorld world = new(legacy, directory);
        world.EnsureField();
        PlayerActionResult entered = world.Apply(new(PlayerActionKind.Confirm));
        Assert.True(entered.Accepted);
        Assert.Equal(PlayerTriggerKind.FieldTransition, entered.Trigger.Kind);
        Assert.Equal(106, entered.Trigger.FieldId);
        Assert.Equal(106, legacy.State.FieldId);
        Assert.Equal((byte)33, legacy.State.SavedWorld!.CarReturn!.EntityType105);
        Assert.Empty(legacy.State.DepartingWorldState!.EventUi!.Windows);
        Assert.Equal((ushort)0x40, legacy.WorldProcess.InputHistory!.CurrentButtons);
        Assert.Equal((ushort)0, legacy.WorldProcess.InputHistory!.PreviousButtons);
    }
}
