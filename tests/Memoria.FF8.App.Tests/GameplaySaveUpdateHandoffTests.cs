using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveUpdateHandoffTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Semantic_handoff_publishes_a_selected_continuation_or_preserves_a_concurrent_destination(bool concurrentDestination)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        GameState before = session.State;
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-gameplay-update-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "continuation.sav");
        int validations = 0;
        UpdateSaveHandoff handoff = new(session.PrepareSnapshot, candidate =>
        {
            validations++;
            Assert.Equal(before.Economy!.Gil, candidate.Economy!.Gil);
            if (concurrentDestination)
            {
                File.WriteAllText(path, "Concurrent destination.");
            }
        }, () => { }, writeGameplay: world.WriteGameplaySave);
        try
        {
            if (concurrentDestination)
            {
                Assert.Throws<IOException>(() => handoff.Save(path, DateTimeOffset.UtcNow.AddMinutes(1)));
                Assert.Equal("Concurrent destination.", File.ReadAllText(path));
                Assert.False(handoff.Paused);
            }
            else
            {
                Assert.True(handoff.Save(path, DateTimeOffset.UtcNow.AddMinutes(1)).Saved);
                GameState loaded = PlayerStateLoader.Load(path, directory, sharedAssets: assets);
                Assert.Equal(before.World!.X, loaded.World!.X);
                Assert.Equal(before.World.Z, loaded.World.Z);
                Assert.True(handoff.Paused);
            }
            Assert.Equal(1, validations);
            Assert.Same(before, session.State);
            Assert.Equal([path], Directory.GetFiles(folder));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
