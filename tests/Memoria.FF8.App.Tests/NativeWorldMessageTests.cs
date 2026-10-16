using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Events;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldMessageTests
{
    [Fact]
    public void ActualTutorialRestoresAndClosesOnlyThroughAuthoredConfirmCommands()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "The installed original world resources are required.");
        string source = TestResources.FixturePath("App.Tests.NativeWorldMessageTests.1");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 30)).Accepted);
        Assert.NotNull(world.CurrentDialogue);
        Assert.Equal(52, world.CurrentDialogue.MessageId);
        Assert.Equal(14, session.State.World!.EventUi!.ScriptMode);
        Assert.True(session.State.SavedWorld!.WorldEventFlags116!.Matches(0x013F));
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, directory, validateWorldPresentation: false);
        restored.EnsureField();
        foreach (PlayerAction action in new[] { new PlayerAction(PlayerActionKind.Wait), new PlayerAction(PlayerActionKind.Confirm), new PlayerAction(PlayerActionKind.Wait) })
        {
            Assert.Equal(world.Apply(action), restored.Apply(action));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        }
        Assert.Null(world.CurrentDialogue);
        Assert.Empty(session.State.World!.EventUi!.Windows);
        Assert.Equal(0, session.State.World.EventUi.ScriptMode);
        Assert.True(session.State.SavedWorld!.WorldEventFlags116!.Matches(0x003F));
    }

    [Fact]
    public void OriginalRagnarokMessageUsesItsPhysicalWindowAndRetainsUnknownTextReadiness()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "The installed original world resources are required.");
        using WorldAssets assets = new(directory);
        NativeWorldMessage message = assets.ReadNativeWorldMessage(9, 52);
        Assert.Equal(0, message.Channel);
        Assert.Equal(0, message.Alignment);
        Assert.Equal(0, message.Mode);
        Assert.Equal(32, message.X);
        Assert.Equal(16, message.Y);
        Assert.Contains("Spaceship Controls", assets.ReadWorldMessage(message.MessageId));
        Assert.Contains("{Key Confirm}", assets.ReadWorldMessage(message.MessageId));
        NativeWorldEventUi opened = NativeWorldEventUi.Create().WithScriptMode(14).SuppressClose().Show(message);
        Assert.False(opened.MatchesTextComplete(9, 1));
        NativeWorldEventUi next = opened.BeginCallback();
        Assert.Throws<InvalidDataException>(() => (message with { TextComplete = true }).Validate());
        Assert.Null(next.MatchesTextComplete(9, 1));
        Assert.False(next.CloseSuppressedThisCallback);
        Assert.Equal(14, next.ScriptMode);
        Assert.Equal(next, next.Show(message));
        NativeWorldEventUi restored = System.Text.Json.JsonSerializer.Deserialize<NativeWorldEventUi>(System.Text.Json.JsonSerializer.Serialize(next))!;
        restored.Validate();
        Assert.Null(restored.MatchesTextComplete(9, 1));
        NativeWorldEventUi closed = restored.Close(9).Close(11).WithScriptMode(0);
        Assert.Empty(closed.Windows);
        Assert.False(closed.MatchesTextComplete(9, 1));
    }
}
