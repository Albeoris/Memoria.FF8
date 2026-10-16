using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class IfritOutsideCheckpointTests
{
    [Fact]
    public void OutsideSnapshotRetainsIfritAndCurrentTimerAcrossQuickAndManualLoad()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        string source = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        GameState recreated = SnapshotFixtures.Require("MEMORIA_FF8_IFRIT_RETURN_SNAPSHOT", 129);
        var session = new GameSession(recreated);
        using var world = new GameWorld(session, gameDirectory);
        world.EnsureField();
        for (int callback = 0; world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable; callback++)
        {
            Assert.True(callback < 3000);
            Assert.Equal(129, world.Events.FieldId);
            Assert.False(world.Events.CurrentDialogue?.IsNamePrompt == true || world.Events.CurrentDialogue?.FirstChoice is not null);
            PlayerAction action = world.Events.CurrentDialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Equal((129, 30), (world.Events.FieldId, session.State.Variables[256]));
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
        Assert.False(world.Events.ExamTimer!.DisplayEnabled);
        Assert.Contains(2, world.Events.AcquiredGuardianForces!);
        Assert.Contains(world.Events.Scene.MinimalMenuCompletions, request => request.RawArgument == 5 && request.GuardianForceId == 2 && request.MenuId == 7);
        NativeBattleCompletion battle = Assert.Single(world.Events.Scene.NativeBattleCompletions!);
        Assert.Equal((94, 4, false, NativeBattleBackend.AutomaticVictory, (byte?)0, 0), ((int)battle.Encounter, (int)battle.Flags, battle.Suppressed, battle.Backend, battle.Result, battle.EmulatedCombatCallbacks));
        Assert.InRange(session.State.Variables[732], 1, 2400);
        Assert.InRange(session.ExamTimer!.RequireRemaining(), 1, 2400);
        world.SetRenderSize(320, 224);
        JsonElement baseline = world.Events.Capture();
        byte[] pixels = world.Render(TimeSpan.Zero);
        session.SetEventState(baseline);
        recreated = session.State;
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.FF8-ifrit-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PlayerSaveStore(source, temporary);
            foreach (int slot in new[] { 0, 60 })
            {
                store.Save(slot, session);
                var loadedSession = new GameSession(store.Load(slot));
                using var loaded = new GameWorld(loadedSession, gameDirectory);
                loaded.EnsureField();
                loaded.SetRenderSize(320, 224);
                Assert.True(JsonElement.DeepEquals(baseline, loaded.Events.Capture()));
                Assert.Equal(pixels, loaded.Render(TimeSpan.Zero));
                var comparisonSession = new GameSession(recreated);
                using var comparison = new GameWorld(comparisonSession, gameDirectory);
                comparison.EnsureField();
                comparison.SetRenderSize(320, 224);
                PlayerAction action = new(PlayerActionKind.Wait);
                Assert.Equal(comparison.Apply(action), loaded.Apply(action));
                Assert.True(JsonElement.DeepEquals(comparison.Events.Capture(), loaded.Events.Capture()));
                Assert.Equal(comparison.Render(TimeSpan.Zero), loaded.Render(TimeSpan.Zero));
            }
            Assert.Equal(original, File.ReadAllBytes(source));
        }
        finally
        {
            DirectoryInfo resolved = new(temporary);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved.FullName, StringComparison.OrdinalIgnoreCase);
            if (resolved.Exists)
            {
                resolved.Delete(true);
            }
        }
    }
}
