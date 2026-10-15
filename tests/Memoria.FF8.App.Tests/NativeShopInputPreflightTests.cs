using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeShopInputPreflightTests
{
    [Theory]
    [InlineData(191, null)]
    [InlineData(191, 0)]
    [InlineData(191, -1)]
    [InlineData(191, 256)]
    [InlineData(-1, 1)]
    [InlineData(256, 1)]
    public void InvalidPurchaseMetadataRefusesBeforeOpeningTheReplayTransaction(int itemId, int? quantity)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine events = new(session, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        GameState originalState = session.State;
        ReplayJournal originalJournal = session.ReplayJournal!;
        JsonElement before = JsonSerializer.SerializeToElement(originalState);
        int journalChanges = 0;
        session.ReplayJournalChanged += _ => journalChanges++;

        Assert.Throws<ArgumentException>(() => events.Apply(new PlayerAction(PlayerActionKind.BuyShopItem, itemId, Quantity: quantity)));

        Assert.Same(originalState, session.State);
        Assert.Same(originalJournal, session.ReplayJournal);
        Assert.True(JsonElement.DeepEquals(before, JsonSerializer.SerializeToElement(session.State)));
        Assert.True(session.ReplayJournal!.IsComplete);
        Assert.Equal(0, session.ReplayJournal.EntryCount);
        Assert.Equal(0, journalChanges);
    }
}
