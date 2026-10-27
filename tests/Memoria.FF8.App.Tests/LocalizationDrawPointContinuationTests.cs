using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationDrawPointContinuationTests
{
    [Fact]
    public void InstalledDrawPointKeepsCommonMessagesThroughSelectionAndAcquisition()
    {
        string directory = TestResources.GameDirectory;
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.LocalizationDrawPointContinuationTests.1")));
        using var resources = new GameResourceHost(directory, watch: false);
        using var world = new GameWorld(session, directory, resources: resources.Resources, resolveGameFile: resources.ResolveGameFile, readSceneMods: () => resources.SceneMods, modelOverrides: resources);
        world.EnsureField();
        RecreateGame host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        Set(host, "session", session);
        Set(host, "resourceHost", resources);
        Set(host, "world", world);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Interact, 14)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 120)).Accepted);
        Assert.Equal(331, world.Events.FieldId);
        VerifyPresentation(host, world);
        Save(session, world, "draw-point-active.json");
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 120)).Accepted);
        DialogueState selection = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.NotNull(selection.FirstChoice);
        VerifyPresentation(host, world);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, selection.InitialChoice!.Value)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 900)).Accepted);
        VerifyPresentation(host, world);
        Assert.NotNull(world.Events.Scene.DrawPointInteraction!.Acquired);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 120)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Null(world.Events.Scene.DrawPointInteraction);
        Assert.Null(world.CurrentDialogue);
        Save(session, world, "draw-point-complete.json");
    }

    private static void VerifyPresentation(RecreateGame host, GameWorld world)
    {
        DialogueState native = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.Equal(-1, native.MessageId);
        world.Events.Scene.DialogueWindows.TryGetValue(native.Channel, out DialogueWindow? window);
        JsonElement before = world.Events.Capture();
        Assert.Same(native, Invoke<DialogueState>(host, "LocalizedDialogue", native));
        Assert.Same(window, Invoke<DialogueWindow?>(host, "LocalizedWindow", native, window));
        Assert.Equal(native.InitialChoice ?? 0, Invoke<int>(host, "LocalizedChoiceLine", native, native.InitialChoice ?? 0));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
    }

    private static void Save(GameSession session, GameWorld world, string name)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_CONTINUATION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(directory);
        session.SetEventState(world.Events.Capture());
        session.Save(Path.Combine(directory, name), compact: true);
        File.WriteAllText(Path.Combine(directory, Path.GetFileNameWithoutExtension(name) + ".summary.json"), JsonSerializer.Serialize(new { Field = world.Events.FieldId, world.Events.Frame, Dialogue = world.CurrentDialogue, Interaction = world.Events.Scene.DrawPointInteraction }));
    }

    private static T Invoke<T>(RecreateGame host, string method, params object?[] arguments)
    {
        return (T)typeof(RecreateGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, arguments)!;
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
