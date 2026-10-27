using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStorageGraphTests
{
    private const int SourceOffset = 0x1000;

    [Fact]
    public void Battle_json_restores_bidirectional_node_main_and_task_views_with_independent_owners()
    {
        BattleSession live = Session();
        BattleSnapshot captured = live.Capture();
        NativeBattleStorageAuthority authority = Assert.Single(captured.StorageGraph!.Authorities);
        Assert.Null(authority.Content);
        string json = JsonSerializer.Serialize(captured);
        BattleSession first = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        NativeBattleEffectArena main = first.NativeEffectMemory!.Arena!;
        NativeBattleNodeSource source = first.NativeEffectMemory.Recover93!.Nodes.Data!;
        Assert.Same(main.Working, source.Arena.Working);
        main.Write(SourceOffset + 4, [71]);
        Assert.Equal((byte)71, source.ReadByte(4));
        source.Arena.Write(5, [72]);
        Assert.Equal((byte)72, main.ReadByte(SourceOffset + 5));
        NativeBattleEffectTaskPool task = NativeBattleEffectTaskPool.Initialize(0x3000000, 0x20DFAB8 + SourceOffset + 64, 32, 1).BindArena(main, NativeBattleTaskStorageBinding.AdoptPhysicalState);
        task.Slots[0].WriteDword(12, 73);
        Assert.Equal(73u, source.ReadUInt32(76));
        source.Arena.Write(80, [74]);
        Assert.Equal((byte)74, task.Slots[0].ReadByte(16));
        Assert.Equal((byte)0, second.NativeEffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        Assert.Equal((byte)0, live.NativeEffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        byte[] raw = new byte[1];
        bool[] known = new bool[1];
        source.ReadWithKnownness(100, raw, known);
        Assert.Equal((byte)123, raw[0]);
        Assert.False(known[0]);
        Assert.Equal(2, source.Arena.ReadResourcePointer(32)!.CpuRow);
        Assert.Equal(json, JsonSerializer.Serialize(captured));
    }

    [Theory]
    [InlineData("range")]
    [InlineData("data")]
    [InlineData("knownness")]
    [InlineData("provenance")]
    [InlineData("footprint")]
    public void Inconsistent_ownership_receipts_are_rejected_before_restore(string corruption)
    {
        JsonObject saved = JsonNode.Parse(JsonSerializer.Serialize(Session().Capture()))!.AsObject();
        JsonObject source = saved["NativeEffectMemory"]!["Recover93"]!["Nodes"]!["Data"]!.AsObject();
        switch (corruption)
        {
            case "range":
                source["StorageReference"]!["Offset"] = NativeBattleEffectArena.Length;
                break;
            case "data":
                source["Data"]![4] = 99;
                break;
            case "knownness":
                source["UnknownRegions"] = new JsonArray();
                break;
            case "footprint":
                saved["StorageGraph"]!["Authorities"]![0]!["DataFootprint"] = new JsonArray();
                break;
            case "provenance":
                source["ResourcePointers"] = new JsonObject();
                break;
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(saved.ToJsonString())!;
        Assert.Throws<InvalidOperationException>(() => BattleSession.Restore(snapshot));
    }

    [Fact]
    public void Legacy_battle_json_without_graph_metadata_keeps_detached_source_data_readable()
    {
        JsonObject saved = JsonNode.Parse(JsonSerializer.Serialize(Session().Capture()))!.AsObject();
        saved.Remove("StorageGraph");
        saved["NativeEffectMemory"]!["Arena"]!.AsObject().Remove("StorageReference");
        saved["NativeEffectMemory"]!["Recover93"]!["Nodes"]!["Data"]!.AsObject().Remove("StorageReference");
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved.ToJsonString())!);
        Assert.Equal(0u, restored.NativeEffectMemory!.Recover93!.Nodes.Data!.ReadUInt32(0));
        Assert.Equal(2, restored.NativeEffectMemory.Recover93.Nodes.Data.Arena.ReadResourcePointer(32)!.CpuRow);
    }

    [Fact]
    public void Choco_returned_source_and_io_keep_one_authority_across_in_memory_capture_restore()
    {
        NativeBattleEffectArena bank = NativeBattleEffectArena.CreateUnknown(64).Write(0, new byte[64]);
        NativeBattleChoco97IoState io = new([], [], 0x1000, new(0x1000, 64, bank));
        NativeBattleGfAnimationSource source = NativeBattleOwnedGfAnimationSource.Own(NativeBattleGfAnimationSource.Borrow(0x1000, bank, 8, 32));
        ChocoAliases frozen = NativeBattleStorageSnapshots.Capture(new ChocoAliases(io, source));
        ChocoAliases first = NativeBattleStorageSnapshots.Restore(frozen);
        ChocoAliases second = NativeBattleStorageSnapshots.Restore(frozen);
        first.Io.Banks.Bytes.Write(12, [81]);
        using (NativeBattleGfSourceReader reader = first.Source.OpenReader())
        {
            Assert.Equal((byte)81, reader.Byte(4));
        }
        using (NativeBattleOwnedGfAnimationSource.Mutation mutation = ((NativeBattleOwnedGfAnimationSource)first.Source).BeginMutation())
        {
            mutation.View.Write(5, [82]);
            mutation.Commit();
        }
        Assert.Equal((byte)82, first.Io.Banks.Bytes.ReadByte(13));
        Assert.Equal((byte)0, second.Io.Banks.Bytes.ReadByte(12));
        Assert.Equal((byte)0, frozen.Io.Banks.Bytes.ReadByte(12));
    }

    [Fact]
    public void Rinoa_json_rebinds_current_node_source_to_current_staging_without_touching_history()
    {
        BattleSession live = SessionWithStaging();
        BattleSnapshot captured = live.Capture();
        string json = JsonSerializer.Serialize(captured);
        BattleSession first = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        NativeBattleNodeSource source = first.NativeEffectMemory!.Rinoa88!.NodeData!;
        Assert.True(source.IsStaging);
        uint address = NativeBattleRinoa88NodeResources.Address;
        first.NativeFileStaging!.Write(address + 4, [91]);
        Assert.Equal((byte)91, source.ReadByte(4));
        source.StagingAuthority.Write(address + 5, [92]);
        Assert.Equal((byte)92, first.NativeFileStaging.ReadByte(address + 5));
        Assert.Equal((byte)0, second.NativeEffectMemory!.Rinoa88!.NodeData!.ReadByte(4));
        Assert.Equal((byte)0, live.NativeEffectMemory!.Rinoa88!.NodeData!.ReadByte(4));
        byte[] raw = new byte[1];
        bool[] known = new bool[1];
        source.ReadWithKnownness(100, raw, known);
        Assert.Equal((byte)123, raw[0]);
        Assert.False(known[0]);
        Assert.Equal(json, JsonSerializer.Serialize(captured));
    }

    [Fact]
    public void Rinoa_json_rejects_node_data_that_disagrees_with_current_staging()
    {
        JsonObject saved = JsonNode.Parse(JsonSerializer.Serialize(SessionWithStaging().Capture()))!.AsObject();
        saved["NativeEffectMemory"]!["Rinoa88"]!["NodeData"]!["Data"]![4] = 93;
        Assert.Throws<InvalidOperationException>(() => BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved.ToJsonString())!));
    }

    private static BattleSession SessionWithStaging()
    {
        BattleSession session = Session();
        uint address = NativeBattleRinoa88NodeResources.Address;
        NativeBattleFileStaging staging = new([]);
        staging.Write(address, new byte[NativeBattleRinoa88NodeResources.Length]);
        staging.Write(address + 100, [123]);
        staging.MarkUnknown(address + 100, 1);
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(staging, address, NativeBattleRinoa88NodeResources.Length);
        NativeBattleEffectMemory memory = session.NativeEffectMemory! with { Rinoa88 = NativeBattleRinoa88State.FromColdProcess() with { NodeData = source } };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeFileStaging))!.SetValue(session, staging);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, memory);
        return session;
    }

    private sealed record ChocoAliases(NativeBattleChoco97IoState Io, NativeBattleGfAnimationSource Source);

    private static BattleSession Session()
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!.BeginLoad(0);
        NativeBattleEffectArena arena = memory.Arena!;
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(arena, SourceOffset, NativeBattleRecover93Nodes.SourceLength);
        arena.Write(SourceOffset + 100, [123]);
        arena.MarkUnknown(SourceOffset + 100, 1);
        source.Arena.WriteResourcePointer(32, new(null, null) { CpuRow = 2 });
        NativeBattleRecover93NodeState nodes = NativeBattleRecover93NodeState.FromColdProcess() with { Data = source };
        memory = memory with { Recover93 = new(null, [], null, null, null, 0, 0, nodes) };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, memory);
        return session;
    }
}
