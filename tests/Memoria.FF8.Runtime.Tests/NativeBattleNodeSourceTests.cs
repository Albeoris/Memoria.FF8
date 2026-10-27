using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNodeSourceTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("\"\"")]
    public void Pending_resources_accept_both_explicit_empty_byte_encodings(string bytes)
    {
        PendingHistory saved = JsonSerializer.Deserialize<PendingHistory>("{\"Source\":" + bytes + "}")!;

        Assert.Null(saved.Source);
        Assert.Equal("{\"Source\":[]}", JsonSerializer.Serialize(saved));
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<History>("{\"Source\":" + bytes + "}"));
    }

    [Fact]
    public void Pending_resources_reject_null_and_preserve_materialized_bytes()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PendingHistory>("{\"Source\":null}"));
        PendingHistory saved = JsonSerializer.Deserialize<PendingHistory>("{\"Source\":\"AQIDBA==\"}")!;

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, saved.Source!.Data);
        PendingHistory restored = NativeBattleStorageSnapshots.Restore(saved);
        Assert.Equal((byte)4, restored.Source!.ReadByte(3));
    }

    [Fact]
    public void Compact_byte_encoding_roundtrips_as_detached_storage()
    {
        JsonSerializerOptions options = new() { Converters = { new ImmutableBytesJsonConverter() } };
        History frozen = NativeBattleStorageSnapshots.Capture(new History(new([1, 2, 3, 4])));
        string json = JsonSerializer.Serialize(frozen, options);

        Assert.Equal("{\"Source\":\"AQIDBA==\"}", json);
        History saved = JsonSerializer.Deserialize<History>(json, options)!;
        Assert.Equal(frozen.Source.Data, saved.Source.Data);
        History first = NativeBattleStorageSnapshots.Restore(saved);
        History second = NativeBattleStorageSnapshots.Restore(saved);
        first.Source.Arena.Write(0, [99]);
        Assert.Equal((byte)1, second.Source.ReadByte(0));
        Assert.Equal((byte)1, saved.Source.Data[0]);
    }

    [Fact]
    public void Legacy_array_shape_is_detached_and_two_restores_have_independent_authorities()
    {
        ImmutableArray<byte> bytes = [1, 2, 3, 4, 5, 6, 7, 8];
        History live = new(new(bytes));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(live));
        History frozen = NativeBattleStorageSnapshots.Capture(live);
        Assert.Equal("{\"Source\":[1,2,3,4,5,6,7,8]}", JsonSerializer.Serialize(frozen));
        History saved = JsonSerializer.Deserialize<History>(JsonSerializer.Serialize(frozen))!;
        History first = NativeBattleStorageSnapshots.Restore(saved);
        History second = NativeBattleStorageSnapshots.Restore(saved);
        first.Source.Arena.Write(0, [99]);
        Assert.Equal((byte)99, first.Source.ReadByte(0));
        Assert.Equal((byte)1, second.Source.ReadByte(0));
        Assert.Equal(bytes, frozen.Source.Data);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = frozen.Source.Arena;
        });
    }

    [Fact]
    public void Physical_alias_capture_preserves_unknown_backing_pointer_metadata_and_restore_aliases()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(64).Write(0, Enumerable.Range(0, 64).Select(index => (byte)index).ToArray());
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(arena, 12, 32);
        arena.MarkUnknown(16, 1);
        source.Arena.WriteResourcePointer(20, new(null, null) { CpuRow = 2 });
        Aliases frozen = NativeBattleStorageSnapshots.Capture(new Aliases(arena, source));
        Aliases restored = NativeBattleStorageSnapshots.Restore(frozen);
        Assert.Same(restored.Bank.Working, restored.Source.Arena.Working);
        restored.Bank.Write(13, [91]);
        Assert.Equal((byte)91, restored.Source.ReadByte(1));
        Assert.Equal((byte)13, frozen.Source.Data[1]);
        Assert.Equal(2, restored.Source.Arena.ReadResourcePointer(20)!.CpuRow);
        string json = JsonSerializer.Serialize(new History(frozen.Source));
        Assert.Contains("UnknownRegions", json);
        Assert.Contains("ResourcePointers", json);
        History roundtrip = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<History>(json)!);
        byte[] data = new byte[1];
        bool[] known = new bool[1];
        roundtrip.Source.ReadWithKnownness(4, data, known);
        Assert.Equal((byte)16, data[0]);
        Assert.False(known[0]);
        Assert.Throws<NotSupportedException>(() => roundtrip.Source.ReadByte(4));
        Assert.Equal(2, roundtrip.Source.Arena.ReadResourcePointer(20)!.CpuRow);
    }

    [Fact]
    public void Cold_and_legacy_restored_sources_read_the_current_public_overlay()
    {
        NativeBattleNodeSource cold = new([1, 2, 3, 4]);
        History saved = NativeBattleStorageSnapshots.Capture(new History(cold));
        NativeBattleNodeSource restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<History>(JsonSerializer.Serialize(saved))!).Source;
        foreach (NativeBattleNodeSource source in new[] { cold, restored })
        {
            using NativeBattleEffectArenaTransaction transaction = new(source.Arena);
            transaction.View.Write(0, [9]);
            Assert.Equal((byte)9, source.ReadByte(0));
            using NativeBattleNodeBankOperation operation = new(source);
            Assert.Equal((byte)9, operation.ReadByte(0));
        }
    }

    private sealed record History([property: JsonConverter(typeof(NativeBattleNodeSource.Converter))] NativeBattleNodeSource Source);
    private sealed record PendingHistory([property: JsonConverter(typeof(NativeBattlePendingNodeSourceConverter))] NativeBattleNodeSource? Source);
    private sealed record Aliases(NativeBattleEffectArena Bank, [property: JsonConverter(typeof(NativeBattleNodeSource.Converter))] NativeBattleNodeSource Source);
}
