using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveSlotCopyTests
{
    [Fact]
    public void Original_bank_import_uses_the_selected_gameplay_publisher_and_preserves_both_files_on_rejection()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState fixture = GameplaySaveSessionLoader.Restore(GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets), directory, sharedAssets: assets);
        byte[] original = NativePcSaveConverter.Export(fixture, assets.ReadNewGameCharacterVitals(), originalPlaceId: 0);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-bank-import-" + Guid.NewGuid().ToString("N"));
        string native = Path.Combine(folder, "original");
        Directory.CreateDirectory(native);
        PlayerSaveStore store = new(Path.Combine(folder, "diagnostic.json"), Path.Combine(folder, "memoria"), originalDirectory: native, discoverOriginalSaves: false, loadOriginal: path => OriginalPlayerSaveLoader.Load(path, directory, assets), loadMemoria: path => PlayerStateLoader.Load(path, directory, sharedAssets: assets));
        GameState WriteImported(GameState state, string path, Action<GameState>? validate)
        {
            GameSession imported = new(state);
            using GameWorld world = new(imported, directory, sharedAssets: assets);
            world.EnsureField();
            return world.WriteGameplaySave(path, validate);
        }
        try
        {
            File.WriteAllBytes(store.GetPath(101), original);
            store.Save(1, new GameSession(fixture));
            byte[] before = File.ReadAllBytes(store.GetPath(1));
            Assert.Throws<InvalidDataException>(() => store.Transfer(101, 1, _ => throw new InvalidDataException("Rejected."), writeGameplay: WriteImported));
            Assert.Equal(before, File.ReadAllBytes(store.GetPath(1)));
            Assert.Equal(original, File.ReadAllBytes(store.GetPath(101)));
            store.Transfer(101, 1, writeGameplay: WriteImported);
            using (FileStream stored = File.OpenRead(store.GetPath(1)))
            {
                Assert.True(GameplaySaveCodec.HasSignature(stored));
            }
            Assert.Equal(165, store.Load(1).FieldId);
            Assert.Equal(original, File.ReadAllBytes(store.GetPath(101)));
            Assert.Single(Directory.GetFiles(Path.Combine(folder, "memoria")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Memoria_slot_copy_retains_the_source_format_and_bytes_after_validation(bool semantic)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-slot-copy-" + Guid.NewGuid().ToString("N"));
        PlayerSaveStore store = new(Path.Combine(folder, "diagnostic.json"), folder, discoverOriginalSaves: false, loadMemoria: path => PlayerStateLoader.Load(path, directory, sharedAssets: assets));
        try
        {
            store.Save(1, session, writeGameplay: semantic ? world.WriteGameplaySave : null);
            store.Save(2, session);
            byte[] source = File.ReadAllBytes(store.GetPath(1));
            byte[] destination = File.ReadAllBytes(store.GetPath(2));
            Assert.Throws<InvalidDataException>(() => store.Transfer(1, 2, _ => throw new InvalidDataException("Rejected.")));
            Assert.Equal(destination, File.ReadAllBytes(store.GetPath(2)));
            int validations = 0;
            store.Transfer(1, 2, candidate =>
            {
                validations++;
                Assert.Equal(session.State.Economy!.Gil, candidate.Economy!.Gil);
            });
            Assert.Equal(1, validations);
            Assert.Equal(source, File.ReadAllBytes(store.GetPath(1)));
            Assert.Equal(source, File.ReadAllBytes(store.GetPath(2)));
            Assert.Equal(2, Directory.GetFiles(folder).Length);
            Assert.Equal(GameSession.ReadSummary(store.GetPath(1))!.SavedLocalAt, GameSession.ReadSummary(store.GetPath(2))!.SavedLocalAt);
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
