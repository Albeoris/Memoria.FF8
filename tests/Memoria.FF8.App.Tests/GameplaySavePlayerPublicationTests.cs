using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySavePlayerPublicationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Player_store_publishes_semantic_slots_and_rotates_only_after_success(int slot)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-player-publication-" + Guid.NewGuid().ToString("N"));
        string saves = Path.Combine(folder, "saves");
        PlayerSaveStore store = new(Path.Combine(folder, "diagnostic.json"), saves, discoverOriginalSaves: false, loadMemoria: path => PlayerStateLoader.Load(path, directory, sharedAssets: assets));
        store.SetQuickSaveCount(1);
        try
        {
            store.Save(slot, session, writeGameplay: world.WriteGameplaySave);
            string originalPath = store.GetPath(slot);
            byte[] original = File.ReadAllBytes(originalPath);
            File.SetLastWriteTimeUtc(originalPath, DateTime.UtcNow.AddMinutes(1));
            DateTime prior = File.GetLastWriteTimeUtc(originalPath);
            Assert.Throws<InvalidDataException>(() => store.Save(slot, session, _ => throw new InvalidDataException("Rejected."), writeGameplay: world.WriteGameplaySave));
            Assert.Equal(original, File.ReadAllBytes(originalPath));
            Assert.Single(Directory.GetFiles(saves));
            store.Save(slot, session, writeGameplay: world.WriteGameplaySave);
            string finalPath = store.GetPath(slot);
            Assert.Single(Directory.GetFiles(saves));
            if (slot == 0)
            {
                Assert.NotEqual(originalPath, finalPath);
                Assert.False(File.Exists(originalPath));
                Assert.True(File.GetLastWriteTimeUtc(finalPath) > prior);
            }
            GameState loaded = store.Load(slot);
            Assert.Equal(session.State.Economy!.Gil, loaded.Economy!.Gil);
            PlayerSaveEntry entry = Assert.Single(store.ReadEntries(slot == 0), entry => entry.Slot == slot);
            Assert.True(entry.CanLoad);
            Assert.NotNull(entry.SavedLocalAt);
            SaveSummary summary = Assert.IsType<SaveSummary>(GameSession.ReadSummary(finalPath));
            Assert.Equal(summary.SavedLocalAt, entry.SavedLocalAt);
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
