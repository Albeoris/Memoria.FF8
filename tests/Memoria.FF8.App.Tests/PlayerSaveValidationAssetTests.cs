using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PlayerSaveValidationAssetTests
{
    [Fact]
    public void Disposing_shared_validation_worlds_keeps_the_live_world_and_next_restore_usable()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SAVE_PERF_FIXTURE");
        string? gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(source is not null && File.Exists(source) && gameDirectory is not null && Directory.Exists(gameDirectory), "An optional standalone save and installed game resources are required.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source!));
        GameState state = GameSession.Load(source!);
        Assert.Equal(SceneKind.Field, state.Scene);
        using var live = new GameWorld(new GameSession(state), gameDirectory!);
        live.EnsureField();
        live.SetRenderSize(320, 224);
        JsonElement baseline = live.Events.Capture();
        byte[] pixels = live.Render(TimeSpan.Zero).ToArray();

        for (int iteration = 0; iteration < 2; iteration++)
        {
            using (var candidate = new GameWorld(new GameSession(GameSession.Load(source!)), gameDirectory!, sharedAssets: live.ValidationAssets))
            {
                Assert.Same(live.ValidationAssets, candidate.ValidationAssets);
                candidate.EnsureField();
                candidate.SetRenderSize(320, 224);
                Assert.True(JsonElement.DeepEquals(baseline, candidate.Events.Capture()));
                Assert.Equal(pixels, candidate.Render(TimeSpan.Zero));
            }

            Assert.False(live.EnsureField());
            Assert.True(JsonElement.DeepEquals(baseline, live.Events.Capture()));
            Assert.Equal(pixels, live.Render(TimeSpan.Zero));
        }

        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source!)));
    }
}
