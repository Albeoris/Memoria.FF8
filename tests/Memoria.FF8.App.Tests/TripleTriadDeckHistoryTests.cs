using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadDeckHistoryTests
{
    [Fact]
    public void OriginalSelectionContinuesThroughIncompleteDeckCancelAndColdReload()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.TripleTriadDeckHistoryTests.Selection");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(source), "Original selection standalone and installed resources are required.");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        TripleTriadController controller = world.EnsureTripleTriad()!;
        Assert.False(controller.AwaitingRules);
        Assert.Null(controller.Match);
        int limited = controller.Available.First(id => session.CardInventory!.AvailableCount(id) < 5);
        int[] owned = Enumerable.Repeat(limited, session.CardInventory!.AvailableCount(limited)).ToArray();
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        string historyPath = Path.Combine(output, "integration-decks.json");
        File.Delete(historyPath);
        TripleTriadDeckHistory history = new(historyPath);
        history.Record(Enumerable.Repeat(limited, 5).ToArray());
        history.Connect(controller);
        session.Save(Path.Combine(output, "selection-standalone.json"));
        controller.ChooseDeck(0);
        Assert.Equal(owned, controller.Deck);
        controller.Navigate(Memoria.FF8.Ui.UiKey.Cancel);
        Assert.Equal(owned.Take(owned.Length - 1), controller.Deck);
        session.Save(Path.Combine(output, "incomplete-deck-standalone.json"));
        while (controller.Match is null)
        {
            controller.ChooseCard(controller.Available.First(id => controller.Remaining(id) > 0));
        }
        Assert.Equal(controller.Deck, history.Load()[0]);
        string confirmed = Path.Combine(output, "confirmed-deck-standalone.json");
        session.Save(confirmed);
        GameSession restored = new(GameSession.Load(confirmed));
        using GameWorld cold = new(restored, directory);
        cold.EnsureField();
        TripleTriadController reloaded = cold.EnsureTripleTriad()!;
        Assert.Equal(controller.Deck, reloaded.Deck);
        Assert.Equal(controller.Match.InitialHands[0], reloaded.Match!.InitialHands[0]);
        if (reloaded.Match.CurrentPlayer == 1)
        {
            Assert.True(reloaded.AdvanceOpponent());
        }
        else
        {
            reloaded.Play(0, 0);
        }
        Assert.Single(reloaded.Match.Board, card => card is not null);
    }

    [Fact]
    public void HistoryPersistsThreeDistinctCompositionsNewestFirst()
    {
        string path = TestResources.ArtifactFile("recent-decks.json");
        TripleTriadDeckHistory history = new(path);
        Assert.Empty(history.Load());
        history.Record([0, 1, 2, 3, 4]);
        history.Record([5, 6, 7, 8, 9]);
        history.Record([10, 11, 12, 13, 14]);
        history.Record([15, 16, 17, 18, 19]);
        history.Record([9, 8, 7, 6, 5]);
        IReadOnlyList<int[]> loaded = new TripleTriadDeckHistory(path).Load();
        Assert.Equal(3, loaded.Count);
        Assert.Equal([9, 8, 7, 6, 5], loaded[0]);
        Assert.Equal([15, 16, 17, 18, 19], loaded[1]);
        Assert.Equal([10, 11, 12, 13, 14], loaded[2]);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "recent-decks.json.*.tmp"));
    }

    [Fact]
    public void CompletingSelectionRecordsTheOriginalHandAndRestoringDoesNotRecord()
    {
        string path = TestResources.ArtifactFile("confirmed-decks.json");
        TripleTriadDeckHistory history = new(path);
        TripleTriadCard[] definitions = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        Dictionary<int, int> collection = Enumerable.Range(0, 10).ToDictionary(id => id, _ => 1);
        TripleTriadController first = new(definitions, collection, [0, 1, 2, 3, 4], id => id.ToString());
        history.Connect(first);
        foreach (int id in new[] { 0, 1, 2, 3 })
        {
            first.ChooseCard(id);
        }
        Assert.False(File.Exists(path));
        first.ChooseCard(4);
        Assert.Equal([0, 1, 2, 3, 4], Assert.Single(history.Load()));
        TripleTriadController next = new(definitions, collection, [0, 1, 2, 3, 4], id => id.ToString());
        history.Connect(next);
        Assert.Equal([0, 1, 2, 3, 4], Assert.Single(next.RecentDecks));
        string before = File.ReadAllText(path);
        next.RestoreMatch(first.Match!);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[[0,1,2,3]]")]
    [InlineData("[[0,1,2,3,110]]")]
    public void InvalidHistoryIsRejected(string json)
    {
        string path = TestResources.ArtifactFile(Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        Assert.Throws<InvalidDataException>(() => new TripleTriadDeckHistory(path).Load());
    }

    [Fact]
    public void GrayscalePreservesAlphaAndLeavesTheColorSourceIntact()
    {
        PixelImage source = new(2, 1);
        source[0, 0] = new Rgba32(200, 50, 10, 77);
        source[1, 0] = new Rgba32(20, 100, 220, 0);
        PixelImage result = MenuArtworkProvider.ToGrayscale(source);
        Assert.Equal(new Rgba32(90, 90, 90, 77), result[0, 0]);
        Assert.Equal(result[1, 0].R, result[1, 0].G);
        Assert.Equal(result[1, 0].G, result[1, 0].B);
        Assert.Equal((byte)0, result[1, 0].A);
        Assert.Equal(new Rgba32(200, 50, 10, 77), source[0, 0]);
    }
}
