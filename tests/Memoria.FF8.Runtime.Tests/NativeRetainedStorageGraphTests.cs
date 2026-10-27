using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeRetainedStorageGraphTests
{
    private const int Offset = 0x1000;

    [Fact]
    public void Field_retained_JSON_preserves_borrowed_main_aliases_unknown_bytes_and_provenance()
    {
        NativeFieldEyeProcess live = Process();
        NativeFieldEyeProcess frozen = live.CaptureSnapshot();
        Assert.NotNull(frozen.Memory.StorageGraph);
        string json = JsonSerializer.Serialize(frozen);
        NativeFieldEyeProcess first = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        NativeFieldEyeProcess second = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        AssertAliases(first.Memory);
        first.Memory.EffectMemory!.Arena!.Write(Offset + 4, [41]);
        Assert.Equal((byte)41, first.Memory.EffectMemory.Recover93!.Nodes.Data!.ReadByte(4));
        first.Memory.EffectMemory.Recover93.Nodes.Data!.Arena.Write(5, [42]);
        Assert.Equal((byte)42, first.Memory.EffectMemory.Arena.ReadByte(Offset + 5));
        Assert.Equal((byte)0, second.Memory.EffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        Assert.Equal((byte)0, live.Memory.EffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        Assert.Equal(json, JsonSerializer.Serialize(frozen));
    }

    [Fact]
    public void Battle_snapshot_handoff_projects_a_self_contained_retained_graph_without_changing_the_snapshot()
    {
        NativeFieldEyeProcess initial = Process().CaptureSnapshot();
        BattleSession battle = new(new BattleDefinition
        {
            NativeRetainedMemory = initial.Memory,
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        battle.NativeEffectMemory!.Arena!.Write(Offset + 4, [51]);
        BattleSnapshot snapshot = battle.Capture();
        string original = JsonSerializer.Serialize(snapshot);
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.Capture(snapshot, snapshot.Definition.NativeRetainedMemory!.ColdExecutableSha256);
        Assert.NotNull(retained.StorageGraph);
        HashSet<int> ids = retained.StorageGraph!.Authorities.Select(authority => authority.Id).ToHashSet();
        Assert.All(NativeBattleStorageGraphViews.Enumerate(retained).Where(view => view.StorageReference is not null), view => Assert.Contains(view.StorageReference!.AuthorityId, ids));
        Assert.True(ids.IsSubsetOf(snapshot.StorageGraph!.Authorities.Select(authority => authority.Id).ToHashSet()));
        NativeFieldEyeProcess first = JsonSerializer.Deserialize<NativeFieldEyeProcess>(JsonSerializer.Serialize(new NativeFieldEyeProcess(1, retained, null)))!.RestoreOwned();
        AssertAliases(first.Memory);
        Assert.Equal((byte)51, first.Memory.EffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        first.Memory.EffectMemory.Arena!.Write(Offset + 4, [52]);
        Assert.Equal((byte)52, first.Memory.EffectMemory.Recover93!.Nodes.Data!.ReadByte(4));
        Assert.Equal((byte)51, retained.EffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        Assert.Equal(original, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public void Invalid_retained_reference_is_rejected_before_an_existing_field_owner_is_replaced()
    {
        NativeFieldEyeProcess frozen = Process().CaptureSnapshot();
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(frozen))!.AsObject();
        json["Memory"]!["EffectMemory"]!["Recover93"]!["Nodes"]!["Data"]!["StorageReference"]!["Offset"] = NativeBattleEffectArena.Length;
        GameSession session = GameSession.CreateColdProcess();
        NativeBattleRetainedMemory existing = session.ActivateNativeFieldMemory();
        NativeFieldEyeProcess invalid = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json.ToJsonString())!;
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => session.SetNativeFieldEyeOwners(invalid.Memory, null));
        Assert.Contains("physical range", failure.Message, StringComparison.Ordinal);
        Assert.Same(existing, session.CurrentNativeBattleMemory);
    }

    [Fact]
    public void Retained_field_root_rebinds_Rinoa_to_its_own_restored_staging()
    {
        NativeFieldEyeProcess live = Process();
        uint address = NativeBattleRinoa88NodeResources.Address;
        NativeBattleFileStaging staging = live.Memory.FileStaging!;
        staging.Write(address, new byte[NativeBattleRinoa88NodeResources.Length]);
        NativeBattleNodeSource node = NativeBattleNodeSource.Borrow(staging, address, NativeBattleRinoa88NodeResources.Length);
        live = live with { Memory = live.Memory with { EffectMemory = live.Memory.EffectMemory! with { Rinoa88 = NativeBattleRinoa88State.FromColdProcess() with { NodeData = node } } } };
        string json = JsonSerializer.Serialize(live.CaptureSnapshot());
        NativeFieldEyeProcess first = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        NativeFieldEyeProcess second = JsonSerializer.Deserialize<NativeFieldEyeProcess>(json)!.RestoreOwned();
        NativeBattleNodeSource restored = first.Memory.EffectMemory!.Rinoa88!.NodeData!;
        Assert.True(restored.IsStaging);
        first.Memory.FileStaging!.Write(address + 4, [61]);
        Assert.Equal((byte)61, restored.ReadByte(4));
        restored.StagingAuthority.Write(address + 5, [62]);
        Assert.Equal((byte)62, first.Memory.FileStaging.ReadByte(address + 5));
        Assert.Equal((byte)0, second.Memory.EffectMemory!.Rinoa88!.NodeData!.ReadByte(4));
        Assert.Equal((byte)0, live.Memory.EffectMemory!.Rinoa88!.NodeData!.ReadByte(4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_second_graph_root_cannot_change_already_finalized_reference_metadata(bool firstHasAlias)
    {
        NativeBattleRetainedMemory firstLive = Process().Memory;
        if (!firstHasAlias)
        {
            firstLive = firstLive with { EffectMemory = firstLive.EffectMemory! with { Recover93 = null } };
        }
        NativeBattleEffectArena firstArena = firstLive.EffectMemory!.Arena!;
        NativeBattleEffectArena otherArena = NativeBattleEffectArena.Clear().Write(Offset + 4, [71]);
        NativeBattleNodeSource otherSource = NativeBattleNodeSource.Borrow(otherArena, Offset, NativeBattleRecover93Nodes.SourceLength);
        NativeBattleRecover93NodeState nodes = NativeBattleRecover93NodeState.FromColdProcess() with { Data = otherSource };
        NativeBattleRetainedMemory secondLive = firstLive with
        {
            EffectMemory = firstLive.EffectMemory with
            {
                Arena = otherArena,
                Recover93 = new(null, [], null, null, null, 0, 0, nodes),
                Reflect106 = new(0, 0, firstArena, 0),
                Death14 = NativeBattleDeathState.FromColdProcess() with { Module = firstArena }
            }
        };
        NativeBattleStorageCaptureContext context = new();
        NativeBattleRetainedMemory first = context.AttachStorageGraph(context.Map(firstLive));
        string before = JsonSerializer.Serialize(first);
        NativeBattleRetainedMemory secondPending = context.Map(secondLive);
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => context.AttachStorageGraph(secondPending));
        Assert.Contains("only one persistence root", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, JsonSerializer.Serialize(first));
        NativeBattleRetainedMemory restoredFirst = new NativeFieldEyeProcess(1, JsonSerializer.Deserialize<NativeBattleRetainedMemory>(before)!, null).RestoreOwned().Memory;
        Assert.Equal((byte)0, restoredFirst.EffectMemory!.Arena!.ReadByte(Offset + 4));
        if (firstHasAlias)
        {
            AssertAliases(restoredFirst);
        }
        NativeBattleRetainedMemory independent = NativeBattleStorageSnapshots.Capture(secondLive);
        NativeBattleRetainedMemory restoredSecond = new NativeFieldEyeProcess(1, JsonSerializer.Deserialize<NativeBattleRetainedMemory>(JsonSerializer.Serialize(independent))!, null).RestoreOwned().Memory;
        Assert.Equal((byte)71, restoredSecond.EffectMemory!.Recover93!.Nodes.Data!.ReadByte(4));
        Assert.Same(restoredSecond.EffectMemory.Arena!.Working, restoredSecond.EffectMemory.Recover93.Nodes.Data.Arena.Working);
        Assert.Same(restoredSecond.EffectMemory.Reflect106!.Packets.Working, restoredSecond.EffectMemory.Death14!.Module.Working);
        Assert.NotSame(restoredSecond.EffectMemory.Arena.Working, restoredSecond.EffectMemory.Reflect106.Packets.Working);
        Assert.Equal(before, JsonSerializer.Serialize(first));
    }

    private static NativeFieldEyeProcess Process()
    {
        NativeBattleRetainedMemory memory = new NativeFieldEyeProcess(1, NativeBattleRetainedMemory.FromColdProcess(), null).RestoreOwned().Memory;
        NativeBattleEffectMemory effects = memory.EffectMemory!.BeginLoad(0);
        NativeBattleEffectArena arena = effects.Arena!;
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(arena, Offset, NativeBattleRecover93Nodes.SourceLength);
        arena.Write(Offset + 100, [0xA5]);
        arena.MarkUnknown(Offset + 100, 1);
        source.Arena.WriteResourcePointer(32, new(null, null) { CpuRow = 2 });
        NativeBattleRecover93NodeState nodes = NativeBattleRecover93NodeState.FromColdProcess() with { Data = source };
        effects = effects with { Recover93 = new(null, [], null, null, null, 0, 0, nodes) };
        return new(1, memory with { EffectMemory = effects }, null);
    }

    private static void AssertAliases(NativeBattleRetainedMemory memory)
    {
        NativeBattleNodeSource source = memory.EffectMemory!.Recover93!.Nodes.Data!;
        Assert.Same(memory.EffectMemory.Arena!.Working, source.Arena.Working);
        byte[] raw = new byte[1];
        bool[] known = new bool[1];
        source.ReadWithKnownness(100, raw, known);
        Assert.Equal((byte)0xA5, raw[0]);
        Assert.False(known[0]);
        Assert.Equal(2, source.Arena.ReadResourcePointer(32)!.CpuRow);
    }
}
