using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DebugViewTests
{
    [Fact]
    public void Typed_debug_view_hides_moves_and_animates_without_changing_story_and_restores_exactly()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? root = new(TestResources.FixtureDirectory);
        string source = Path.Combine(root!.FullName, "../fixtures/external-fixture.json");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        string native = world.Events.Capture().GetRawText();
        byte[] baseline = world.Render(TimeSpan.Zero).ToArray();
        ActorState actor = Assert.Single(world.Events.Actors, actor => actor.CharacterId == 0);
        int animation = Enumerable.Range(0, world.Field!.Models[actor.ModelId].Animations.Count).First(id => world.Field.GetAnimationFrameCount(actor.ModelId, id) > 2);
        string queue = Path.Combine(Path.GetTempPath(), "memoria-debug-" + Guid.NewGuid().ToString("N"));
        var controls = new ControlQueue(queue, request =>
        {
            world.ApplyDebug(request.Debug!);
            return session.State.DebugView!;
        });
        try
        {
            File.WriteAllText(Path.Combine(queue, "requests", "hide.json"), "{\"Id\":\"hide\",\"Command\":\"debug-scene\",\"Debug\":{\"Target\":\"Dialogue\",\"Visible\":false}}");
            controls.Poll();
            Assert.Contains("\"Ok\": true", File.ReadAllText(Path.Combine(queue, "responses", "hide.json")));
            Assert.False(session.State.DebugView!.DialogueVisible);
            Assert.True(world.Events.IsDialogueOpen);
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Background, ModuleId: 3, Visible: false));
            Assert.False(baseline.SequenceEqual(world.Render(TimeSpan.Zero)));
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Actor, ActorId: actor.ObjectId, X: actor.X + 500, Z: actor.Z + 40, AnimationId: animation, Frame: 0, Playing: true, FramesPerSecond: 8));
            world.AdvanceDebug(TimeSpan.FromSeconds(0.25));
            Assert.Equal(2, session.State.DebugView.Actors[actor.ObjectId].Frame);
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Actor, ActorId: actor.ObjectId, Visible: false));
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Background, Visible: false));
            Assert.False(baseline.SequenceEqual(world.Render(TimeSpan.Zero)));
            Assert.Equal(native, world.Events.Capture().GetRawText());
            Assert.Empty(world.Events.ActionLog);
            Assert.All(world.DebugActions, action => Assert.Equal("DebugPresentation", action.Mode));
            session.SetEventState(world.Events.Capture());
            GameState saved = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
            var restoredSession = new GameSession(saved);
            using var restored = new GameWorld(restoredSession, directory);
            restored.EnsureField();
            Assert.Equal(JsonSerializer.Serialize(session.State.DebugView), JsonSerializer.Serialize(restoredSession.State.DebugView));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Clear));
            Assert.Null(session.State.DebugView);
            Assert.Equal(baseline, world.Render(TimeSpan.Zero));
            Assert.Equal(native, world.Events.Capture().GetRawText());
            Assert.Throws<ArgumentOutOfRangeException>(() => world.ApplyDebug(new DebugSceneCommand(DebugSceneTarget.Background, ModuleId: 256, Visible: false)));
            Assert.Null(session.State.DebugView);
        }
        finally
        {
            Directory.Delete(queue, true);
        }
    }
}
