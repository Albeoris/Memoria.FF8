using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CompletedGameStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria-completed-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CompletedManualSaveSurvivesDiscoveryAndRejectsAutomaticReplacement()
    {
        PlayerSaveStore store = Store();
        store.SaveCompleted(1, CompletedSaveFixture.Document());
        byte[] original = File.ReadAllBytes(store.GetPath(1));
        PlayerSaveEntry entry = Assert.Single(store.ReadCompletedEntries());
        Assert.True(entry.IsCompletedGame);
        Assert.True(entry.CanLoad);
        Assert.Throws<InvalidOperationException>(() => store.Save(1, Ordinary()));
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(1)));
        Assert.Single(Store().ReadCompletedEntries());
        Assert.Throws<InvalidOperationException>(() => store.SaveCompleted(0, CompletedSaveFixture.Document()));
        Assert.Throws<InvalidOperationException>(() => store.SaveCompleted(PlayerSaveStore.MemoriaSlotCount + 1, CompletedSaveFixture.Document()));
    }

    [Fact]
    public void ReviewedReplacementRemovesCompletionAndApprovalIsBoundToDestinationBytes()
    {
        PlayerSaveStore store = Store();
        store.SaveCompleted(1, CompletedSaveFixture.Document());
        string reviewed = Assert.IsType<string>(store.CompletedRevision(1));
        GameplaySaveComponentDocument replacement = CompletedSaveFixture.Document();
        using (FileStream stream = File.Create(store.GetPath(1)))
        {
            GameplaySaveCodec.Write(stream, replacement);
        }
        Assert.Throws<InvalidOperationException>(() => store.ApproveCompletedOverwrite(1, reviewed));
        store.ApproveCompletedOverwrite(1, store.CompletedRevision(1)!);
        store.Save(1, Ordinary());
        Assert.Empty(store.ReadCompletedEntries());
        Assert.Null(store.CompletedRevision(1));
        Assert.Null(store.Load(1).CompletedGame);
    }

    [Fact]
    public void DestinationChangeDuringWritePreservesTheNewDestination()
    {
        PlayerSaveStore store = Store();
        store.SaveCompleted(1, CompletedSaveFixture.Document());
        store.ApproveCompletedOverwrite(1, store.CompletedRevision(1)!);
        byte[]? changed = null;
        Assert.Throws<InvalidOperationException>(() => store.Save(1, Ordinary(), writeGameplay: (staging, validate, savedAt) =>
        {
            Ordinary().Save(staging, compact: true);
            using (FileStream destination = File.Create(store.GetPath(1)))
            {
                GameplaySaveCodec.Write(destination, CompletedSaveFixture.Document());
            }
            changed = File.ReadAllBytes(store.GetPath(1));
            return Ordinary().State;
        }));
        Assert.Equal(changed, File.ReadAllBytes(store.GetPath(1)));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void QuickSaveCleanupPreservesCompletedFiles()
    {
        PlayerSaveStore store = Store();
        store.SaveCompleted(1, CompletedSaveFixture.Document());
        string completedQuick = Path.Combine(directory, "quick-0000000001.json");
        File.Copy(store.GetPath(1), completedQuick);
        Ordinary().Save(Path.Combine(directory, "quick-0000000002.json"), compact: true);
        Ordinary().Save(Path.Combine(directory, "quick-0000000003.json"), compact: true);
        store.SetQuickSaveCount(3);
        int count = store.PreviewQuickSaveCount(1);
        store.CommitQuickSaveCount(1, count, () => { });
        Assert.True(File.Exists(completedQuick));
        Assert.Equal(2, store.ReadCompletedEntries().Count);
    }

    [Fact]
    public void MemoriaTransferPreservesTheWholeCompletedSourceAndProtectsItsDestination()
    {
        PlayerSaveStore store = Store();
        store.SaveCompleted(1, CompletedSaveFixture.Document());
        byte[] source = File.ReadAllBytes(store.GetPath(1));
        store.Transfer(1, 2);
        Assert.Equal(source, File.ReadAllBytes(store.GetPath(1)));
        Assert.Equal(source, File.ReadAllBytes(store.GetPath(2)));
        using (FileStream copied = File.OpenRead(store.GetPath(2)))
        {
            GameplaySaveComponentDocument document = GameplaySaveCodec.Read(copied);
            Assert.NotNull(document.CompletedGame);
            Assert.Equal(12345u, document.Characters.Records[0].Experience);
        }
        Assert.Equal(2, store.ReadCompletedEntries().Count);
        Assert.Throws<InvalidOperationException>(() => store.Transfer(1, 2));
        Assert.Equal(source, File.ReadAllBytes(store.GetPath(2)));
    }

    [Fact]
    public void CompletedSemanticsRequireMemoriaFormat()
    {
        GameState state = CompletedSaveFixture.State();
        state = state with { CompletedGame = CompletedSaveFixture.Document().CompletedGame };
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(state));
    }

    [Fact]
    public void CleanupRechecksCompletionAfterThePersistenceCallback()
    {
        PlayerSaveStore store = Store();
        string older = Path.Combine(directory, "quick-0000000001.json");
        Ordinary().Save(older, compact: true);
        Ordinary().Save(Path.Combine(directory, "quick-0000000002.json"), compact: true);
        store.SetQuickSaveCount(2);
        int count = store.PreviewQuickSaveCount(1);
        Assert.Equal(1, count);
        store.CommitQuickSaveCount(1, count, () =>
        {
            using FileStream stream = File.Create(older);
            GameplaySaveCodec.Write(stream, CompletedSaveFixture.Document());
        });
        Assert.True(File.Exists(older));
    }

    [Fact]
    public void RotatingSaveRechecksCompletionBeforeRemovingOldFiles()
    {
        PlayerSaveStore store = Store();
        string older = Path.Combine(directory, "quick-0000000001.json");
        Ordinary().Save(older, compact: true);
        store.SetQuickSaveCount(1);
        store.Save(0, Ordinary(), writeGameplay: (staging, validate, savedAt) =>
        {
            Ordinary().Save(staging, compact: true);
            using FileStream stream = File.Create(older);
            GameplaySaveCodec.Write(stream, CompletedSaveFixture.Document());
            return Ordinary().State;
        });
        Assert.True(File.Exists(older));
    }

    private PlayerSaveStore Store()
    {
        Directory.CreateDirectory(directory);
        return new PlayerSaveStore(Path.Combine(directory, "diagnostic.json"), directory, discoverOriginalSaves: false);
    }

    private static GameSession Ordinary()
    {
        return new GameSession(new GameState { Scene = SceneKind.Field, FieldId = 180 });
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
