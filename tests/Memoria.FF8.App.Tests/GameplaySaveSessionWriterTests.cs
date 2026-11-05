using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveSessionWriterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Publish_replaces_an_old_save_only_after_the_serialized_document_loads(bool world)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, world);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "slot.sav");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);
            GameState validated = GameplaySaveSessionWriter.Write(path, document, directory, sharedAssets: assets);
            using FileStream stored = File.OpenRead(path);
            GameplaySaveComponentDocument published = GameplaySaveCodec.Read(stored);
            Assert.Equal(document.Resume, published.Resume);
            Assert.Equal(document.Economy.Gil, validated.Economy!.Gil);
            Assert.Equal(world ? SceneKind.WorldMap : SceneKind.Field, validated.Scene);
            GameState selected = PlayerStateLoader.Load(path, sharedAssets: assets);
            Assert.Equal(validated.Scene, selected.Scene);
            Assert.Equal(validated.Economy.Gil, selected.Economy!.Gil);
            SaveSummary summary = Assert.IsType<SaveSummary>(GameSession.ReadSummary(path));
            Assert.Equal(selected.Scene, summary.Scene);
            Assert.Equal(document.Resume.FieldId, summary.FieldId);
            Assert.Equal(document.Progression!.Clocks.Playtime!.Seconds, summary.Playtime!.Seconds);
            PlayerSaveStore store = new(Path.Combine(folder, "diagnostic.json"), folder, discoverOriginalSaves: false);
            File.Copy(path, store.GetPath(1));
            PlayerSaveEntry entry = Assert.Single(store.ReadEntries(false), entry => entry.Slot == 1);
            Assert.True(entry.CanLoad);
            Assert.Equal(TimeSpan.FromSeconds(summary.Playtime.Seconds), entry.PlayTime);
            File.Delete(store.GetPath(1));
            Assert.Equal([path], Directory.GetFiles(folder));
            Assert.NotNull(assets.LoadField(165));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_normal_load_preserves_existing_destination_and_removes_only_its_temporary_file(bool existing)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, true);
        document = document with { Resume = document.Resume with { World = document.Resume.World! with { EntityType = 16 } } };
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "slot.sav");
        string unrelated = Path.Combine(folder, ".slot.sav.unrelated.tmp");
        byte[] original = [8, 7, 6, 5];
        try
        {
            if (existing)
            {
                File.WriteAllBytes(path, original);
            }
            File.WriteAllBytes(unrelated, original);
            Assert.Throws<NotSupportedException>(() => GameplaySaveSessionWriter.Write(path, document, directory, sharedAssets: assets));
            Assert.Equal(existing, File.Exists(path));
            if (existing)
            {
                Assert.Equal(original, File.ReadAllBytes(path));
            }
            Assert.Equal(original, File.ReadAllBytes(unrelated));
            Assert.Equal(existing ? 2 : 1, Directory.GetFiles(folder).Length);
            Assert.NotNull(assets.LoadField(165));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
