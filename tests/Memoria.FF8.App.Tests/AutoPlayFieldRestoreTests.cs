using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayFieldRestoreTests
{
    [Fact]
    public void NeutralSameFieldReloadMatchesColdNativeOwnersAndNextCallbackWithFreshDiagnostics()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayObservedInputTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState run = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, run.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        GameState source = GameSession.Load(snapshot);
        GameSession warmSession = GameSession.CreateColdProcess();
        warmSession.Restore(source);
        using GameWorld warm = new(warmSession, TestResources.GameDirectory, validateWorldPresentation: false);
        warm.EnsureField();
        warmSession.SetEventState(warm.Events.Capture());
        GameState checkpoint = warmSession.State;
        Assert.Equal(902, checkpoint.FieldId);
        Assert.Equal(395, warm.Events.Frame);
        var field = warm.Field;
        warm.Apply(new(PlayerActionKind.FieldButtons, Value: 0));
        warm.AdvanceAutoPlayInputFrame();
        Assert.NotEmpty(warm.Events.ActionLog);

        long reloadStarted = Stopwatch.GetTimestamp();
        Assert.True(warm.TryRestoreAutoPlayFieldSnapshot(checkpoint));
        double reloadMilliseconds = Stopwatch.GetElapsedTime(reloadStarted).TotalMilliseconds;
        Assert.Same(field, warm.Field);
        Assert.Empty(warm.Events.ActionLog);
        Assert.Empty(warm.Events.Coverage);
        Assert.Empty(warm.Events.Decisions);
        Assert.Empty(warm.Events.DialogueOutcomes);
        Assert.Empty(warm.Events.LineCrossings);

        long coldStarted = Stopwatch.GetTimestamp();
        GameSession coldSession = GameSession.CreateColdProcess();
        coldSession.Restore(checkpoint);
        using GameWorld cold = new(coldSession, TestResources.GameDirectory, validateWorldPresentation: false);
        cold.EnsureField();
        double coldMilliseconds = Stopwatch.GetElapsedTime(coldStarted).TotalMilliseconds;
        AssertEquivalent(warmSession, warm, coldSession, cold);

        PlayerAction action = new(PlayerActionKind.Wait, Frames: 1);
        Assert.Equal(warm.Apply(action), cold.Apply(action));
        AssertEquivalent(warmSession, warm, coldSession, cold);
        Assert.Equal(1, Assert.Single(warm.Events.ActionLog).Index);
        Assert.Equal(JsonSerializer.Serialize(warm.Events.ActionLog), JsonSerializer.Serialize(cold.Events.ActionLog));
        Assert.Equal(JsonSerializer.Serialize(warm.Events.Decisions), JsonSerializer.Serialize(cold.Events.Decisions));
        Assert.Equal(JsonSerializer.Serialize(warm.Events.DialogueOutcomes), JsonSerializer.Serialize(cold.Events.DialogueOutcomes));
        Assert.Equal(JsonSerializer.Serialize(warm.Events.Coverage), JsonSerializer.Serialize(cold.Events.Coverage));

        GameState beforeRejected = warmSession.State;
        JsonElement vmBeforeRejected = warm.Events.Capture();
        Assert.False(warm.TryRestoreAutoPlayFieldSnapshot(checkpoint with { FieldId = checkpoint.FieldId + 1 }));
        Assert.False(warm.TryRestoreAutoPlayFieldSnapshot(checkpoint with { Scene = SceneKind.Movie }));
        Assert.False(warm.TryRestoreAutoPlayFieldSnapshot(checkpoint with { FieldRequestPacket = new NativeFieldRequestPacket { Kind = 4 } }));
        Assert.Same(beforeRejected, warmSession.State);
        Assert.True(JsonElement.DeepEquals(vmBeforeRejected, warm.Events.Capture()));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        Console.WriteLine($"Same-field cached-resource reload {reloadMilliseconds:F2}ms; independent cold host {coldMilliseconds:F2}ms; exact owners and next callback passed.");
    }

    private static void AssertEquivalent(GameSession warmSession, GameWorld warm, GameSession coldSession, GameWorld cold)
    {
        JsonElement warmVm = warm.Events.Capture();
        JsonElement coldVm = cold.Events.Capture();
        Assert.True(JsonElement.DeepEquals(warmVm, coldVm));
        warmSession.SetEventState(warmVm);
        coldSession.SetEventState(coldVm);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(warmSession.State), JsonSerializer.SerializeToElement(coldSession.State)));
    }
}
