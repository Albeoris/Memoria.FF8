using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ReplayPartySlotTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompactCodecPreservesTheExplicitSlotAndEmptyReserveInput(int slot)
    {
        //Codec-only fixture, without fabricated native field/menu history.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        ReplayJournalEntry selected = new(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 11, Value = slot };
        ReplayJournalEntry empty = new(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 9, Value = 255 };
        session.AppendReplayEntry(selected);
        session.AppendReplayEntry(empty);
        ReplayJournal restored = JsonSerializer.Deserialize<ReplayJournal>(JsonSerializer.Serialize(session.ReplayJournal))!;
        Assert.Equal(2, restored.EntryCount);
        Assert.True(restored.IsComplete);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(selected), JsonSerializer.SerializeToElement(restored.Entries[0])));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(empty), JsonSerializer.SerializeToElement(restored.Entries[1])));
    }

    [Theory]
    [InlineData(11, -1)]
    [InlineData(11, 3)]
    [InlineData(11, 256)]
    [InlineData(9, -1)]
    [InlineData(9, 8)]
    [InlineData(9, 254)]
    [InlineData(9, 256)]
    public void CodecRefusesInvalidPartyInputDomains(int code, int value)
    {
        Assert.Throws<InvalidDataException>(() => new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = code, Value = value }.Validate());
    }
}
