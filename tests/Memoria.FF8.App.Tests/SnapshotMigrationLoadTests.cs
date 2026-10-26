using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SnapshotMigrationLoadTests
{
    [Fact]
    public void ConfiguredLegacyFieldSnapshotLoadsSavesAndContinuesInAFreshHost()
    {
        string? sourceSetting = Environment.GetEnvironmentVariable("MEMORIA_FF8_SAVE_MIGRATION_SOURCE");
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(sourceSetting) && !string.IsNullOrWhiteSpace(gameDirectory), "Set MEMORIA_FF8_SAVE_MIGRATION_SOURCE and MEMORIA_FF8_GAME_DIR for installed snapshot migration acceptance.");
        string source = Path.GetFullPath(sourceSetting!);
        Assert.True(File.Exists(source), "Configured migration source must exist.");
        Assert.True(Directory.Exists(gameDirectory), "Configured installed game directory must exist.");
        string? outputSetting = Environment.GetEnvironmentVariable("MEMORIA_FF8_SAVE_MIGRATION_OUTPUT");
        bool temporaryOutput = string.IsNullOrWhiteSpace(outputSetting);
        string output = temporaryOutput ? Path.Combine(Path.GetTempPath(), "Memoria.FF8-migration-" + Guid.NewGuid().ToString("N") + ".ff8") : Path.GetFullPath(outputSetting!);
        Assert.False(string.Equals(source, output, StringComparison.OrdinalIgnoreCase), "Migration output must be distinct from its preserved source.");
        Assert.False(File.Exists(output) || Directory.Exists(output) || File.Exists(output + ".tmp"), "Migration output and its temporary file must not already exist.");
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source));
        try
        {
            GameState loaded = GameSession.Load(source);
            Assert.Equal(SceneKind.Field, loaded.Scene);
            GameSession session = new(loaded);
            using GameWorld world = new(session, gameDirectory!);
            world.EnsureField();
            world.SetRenderSize(320, 224);
            long initialFrame = world.Events.Frame;
            byte[] pixels = world.Render(TimeSpan.Zero);
            Assert.Equal(320 * 224 * 4, pixels.Length);
            session.SetEventState(world.Events.Capture());
            session.Save(output, compact: true);
            Assert.Equal(initialFrame, world.Events.Frame);

            GameSession restoredSession = new(GameSession.Load(output));
            using GameWorld restored = new(restoredSession, gameDirectory!);
            restored.EnsureField();
            restored.SetRenderSize(320, 224);
            Assert.Equal(session.State.FieldId, restoredSession.State.FieldId);
            Assert.Equal(initialFrame, restored.Events.Frame);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(pixels, restored.Render(TimeSpan.Zero));

            PlayerAction next = new(PlayerActionKind.Wait, Frames: 1, WaitWhilePlayable: true);
            PlayerActionResult advanced = world.Apply(next);
            PlayerActionResult restoredAdvanced = restored.Apply(next);
            Assert.True(advanced.Accepted, advanced.Detail);
            Assert.Equal(1, advanced.FramesAdvanced);
            Assert.Equal(advanced, restoredAdvanced);
            Assert.Equal(initialFrame + 1, world.Events.Frame);
            Assert.Null(world.Events.LastFault);
            Assert.Null(restored.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        }
        finally
        {
            if (temporaryOutput)
            {
                File.Delete(output);
            }
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source)));
        }
    }
}
