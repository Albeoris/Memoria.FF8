using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ReplayPurchaseQuantityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void CompactJournalPreservesExplicitPurchaseQuantity(int quantity)
    {
        //Codec-only input fixture; no original menu, field visit or game outcome is claimed.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        ReplayJournalEntry purchase = new(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 10, Value = 191, Quantity = quantity, Trigger = 8, ResultTrigger = 8 };
        session.AppendReplayEntry(purchase);
        string capsule = JsonSerializer.Serialize(session.ReplayJournal);
        ReplayJournal decoded = JsonSerializer.Deserialize<ReplayJournal>(capsule)!;
        ReplayJournalEntry restored = Assert.Single(decoded.Entries);
        Assert.Equal(10, restored.Code);
        Assert.Equal(191, restored.Value);
        Assert.Equal(quantity, restored.Quantity);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(purchase), JsonSerializer.SerializeToElement(restored)));
        JsonArray payload = DecodePayload(JsonNode.Parse(capsule)!);
        Assert.Equal(quantity, payload[0]![nameof(ReplayJournalEntry.Quantity)]!.GetValue<int>());
    }

    [Fact]
    public void MissingCompactPurchaseQuantityRefusesInsteadOfInferringOne()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.AppendReplayEntry(new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 10, Value = 191, Quantity = 1 });
        JsonNode capsule = JsonNode.Parse(JsonSerializer.Serialize(session.ReplayJournal))!;
        JsonArray payload = DecodePayload(capsule);
        Assert.True(payload[0]!.AsObject().Remove(nameof(ReplayJournalEntry.Quantity)));
        using MemoryStream compressed = new();
        using (BrotliStream compressor = new(compressed, CompressionLevel.Optimal, true))
        {
            JsonSerializer.Serialize(compressor, payload);
        }
        capsule["Data"] = Convert.ToBase64String(compressed.ToArray());
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<ReplayJournal>(capsule.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => new ReplayJournalEntry(ReplayEntryKind.Action, SceneKind.Field, 74, 0) { Code = 10, Value = 191 }.Validate());
    }

    private static JsonArray DecodePayload(JsonNode capsule)
    {
        using MemoryStream compressed = new(Convert.FromBase64String(capsule["Data"]!.GetValue<string>()));
        using BrotliStream decompressor = new(compressed, CompressionMode.Decompress);
        return JsonNode.Parse(decompressor)!.AsArray();
    }
}
