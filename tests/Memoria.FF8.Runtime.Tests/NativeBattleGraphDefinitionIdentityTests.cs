using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphDefinitionIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Repeated_definitions_restore_one_wrapper_and_stable_JSON_with_independent_owners(bool throughJson)
    {
        NativeBattleRetainedMemory original = Create("same");
        NativeBattleRetainedMemory captured = NativeBattleStorageSnapshots.Capture(original);
        string json = JsonSerializer.Serialize(captured);
        Assert.True(captured.EffectMemory!.Arena!.StorageReference!.DefinesAuthority);
        Assert.True(captured.EffectMemory.Reflect106!.Packets.StorageReference!.DefinesAuthority);

        NativeBattleRetainedMemory first = Restore(captured, throughJson);
        NativeBattleRetainedMemory second = Restore(captured, throughJson);
        NativeBattleEffectArena main = first.EffectMemory!.Arena!;
        NativeBattleEffectArena alias = first.EffectMemory.Reflect106!.Packets;
        Assert.Same(main, alias);
        Assert.Same(main.Working, alias.Working);
        Assert.NotSame(main, second.EffectMemory!.Arena);
        Assert.NotSame(main.Working, second.EffectMemory.Arena!.Working);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)));
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second)));
        AssertContents(main);

        main.Write(8, [91]);
        alias.WriteResourcePointer(32, new(null, null) { CpuRow = 3 });
        Assert.Equal((byte)91, alias.ReadByte(8));
        Assert.Equal(3, main.ReadResourcePointer(32)!.CpuRow);
        AssertContents(second.EffectMemory.Arena);
        AssertContents(captured.EffectMemory.Arena);
        AssertContents(original.EffectMemory!.Arena!);
        Assert.Equal(json, JsonSerializer.Serialize(captured));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Nondefining_full_views_keep_distinct_wrappers_and_borrowed_routing(bool borrowed, bool throughJson)
    {
        NativeBattleRetainedMemory captured = NativeBattleStorageSnapshots.Capture(Create(borrowed ? "borrowed" : "distinct"));
        string json = JsonSerializer.Serialize(captured);
        Assert.True(captured.EffectMemory!.Arena!.StorageReference!.DefinesAuthority);
        NativeBattleStorageReference reference = captured.EffectMemory.Reflect106!.Packets.StorageReference!;
        Assert.False(reference.DefinesAuthority);
        Assert.Equal(borrowed, reference.Borrowed);

        NativeBattleRetainedMemory restored = Restore(captured, throughJson);
        NativeBattleEffectArena main = restored.EffectMemory!.Arena!;
        NativeBattleEffectArena alias = restored.EffectMemory.Reflect106!.Packets;
        Assert.NotSame(main, alias);
        Assert.Same(main.Working, alias.Working);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        AssertContents(alias);
        using (NativeBattleEffectArenaTransaction transaction = new(main))
        {
            transaction.Write(8, [92]);
            Assert.Equal(borrowed ? (byte)92 : (byte)17, alias.ReadByte(8));
            Assert.Equal((byte)17, main.ReadByte(8));
        }
        AssertContents(alias);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("knownness")]
    [InlineData("pointer")]
    [InlineData("null-pointer")]
    public void A_later_defining_view_is_fully_validated_before_wrapper_reuse(string corruption)
    {
        NativeBattleRetainedMemory captured = NativeBattleStorageSnapshots.Capture(Create("same"));
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(captured))!.AsObject();
        JsonObject duplicate = json["EffectMemory"]!["Reflect106"]!["Packets"]!.AsObject();
        Assert.True(duplicate["StorageReference"]!["DefinesAuthority"]!.GetValue<bool>());
        switch (corruption)
        {
            case "data":
                duplicate["Regions"]![0]!["Data"]![8] = 93;
                break;
            case "knownness":
                JsonArray unknown = duplicate["UnknownRegions"]!.AsArray();
                JsonNode range = Assert.Single(unknown, item => item!["Offset"]!.GetValue<int>() == 18)!;
                Assert.Equal(1, range["Length"]!.GetValue<int>());
                unknown.Remove(range);
                break;
            case "pointer":
                duplicate["ResourcePointers"]!["32"]!["CpuRow"] = 4;
                break;
            case "null-pointer":
                duplicate["ResourcePointers"]!.AsObject().Remove("40");
                break;
        }
        NativeBattleRetainedMemory invalid = JsonSerializer.Deserialize<NativeBattleRetainedMemory>(json.ToJsonString())!;
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Restore(invalid));
        Assert.Contains(corruption is "pointer" or "null-pointer" ? "provenance" : "data disagrees", failure.Message, StringComparison.Ordinal);
        AssertContents(captured.EffectMemory!.Arena!);
    }

    private static NativeBattleRetainedMemory Create(string aliasKind)
    {
        NativeBattleEffectArena main = NativeBattleEffectArena.CreateUnknown(64).Write(0, new byte[64]);
        main.Write(8, [17]);
        main.Write(18, [0xA5]);
        main.MarkUnknown(18, 1);
        main.WriteResourcePointer(32, new(null, null) { CpuRow = 2 });
        main.WriteResourcePointer(40, null);
        NativeBattleEffectArena alias = aliasKind switch
        {
            "same" => main,
            "distinct" => main with { },
            "borrowed" => NativeBattleEffectArena.BorrowWorking(main.Working, 0, main.LogicalLength),
            _ => throw new ArgumentOutOfRangeException(nameof(aliasKind))
        };
        NativeBattleEffectMemory effects = new(main, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0)
        {
            Reflect106 = new(0, 0, alias, 0)
        };
        NativeBattleRetainedMemory retained = NativeBattleStorageSnapshots.Restore(NativeBattleRetainedMemory.FromColdProcess());
        return retained with { EffectMemory = effects };
    }

    private static NativeBattleRetainedMemory Restore(NativeBattleRetainedMemory captured, bool throughJson)
    {
        NativeBattleRetainedMemory detached = throughJson ? JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(captured))! : captured;
        return NativeBattleStorageSnapshots.Restore(detached);
    }

    private static void AssertContents(NativeBattleEffectArena arena)
    {
        Assert.Equal((byte)17, arena.ReadByte(8));
        Span<byte> bytes = stackalloc byte[1];
        Span<bool> known = stackalloc bool[1];
        arena.ReadWithKnownness(18, bytes, known);
        Assert.Equal((byte)0xA5, bytes[0]);
        Assert.False(known[0]);
        Assert.Equal(2, arena.ReadResourcePointer(32)!.CpuRow);
        Assert.True(arena.TryGetResourcePointer(40, out NativeBattleResourcePointer? pointer));
        Assert.Null(pointer);
    }
}
