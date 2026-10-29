using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTaskBankSnapshotOwnershipTests
{
    private const uint ArenaAddress = 0x20DFAB8;
    private const int BoundOffset = 64;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Captured_bank_receipts_preserve_independent_TIM_bytes_and_bound_aliases_across_two_restores(bool json)
    {
        Saved live = Create();
        Saved checkpoint = NativeBattleStorageSnapshots.Capture(live);
        string original = JsonSerializer.Serialize(checkpoint);
        JsonObject document = JsonNode.Parse(original)!.AsObject();
        Assert.False(document["Pools"]![0]!["ArenaBinding"]!.GetValue<bool>());
        Assert.True(document["Pools"]![1]!["ArenaBinding"]!.GetValue<bool>());
        Saved first = Restore(checkpoint, original, json);
        Saved second = Restore(checkpoint, original, json);
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)));
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second)));
        Assert.False(first.Pools[0].IsBoundToStorage(first.Memory.Arena!, ArenaAddress));
        Assert.True(first.Pools[1].IsBoundToStorage(first.Memory.Arena!, ArenaAddress));
        Assert.Equal(new byte[] { 16, 0, 0, 0, 9, 0, 0, 0 }, first.Memory.Arena!.Read(0, 8));
        Assert.Equal((byte)0xA7, NativeBattleEffectTaskMemory.CaptureBank(first.Pools[0]).ReadSnapshot(20, 1).Data[0]);
        Assert.Throws<NotSupportedException>(() => first.Pools[0].Slots[0].ReadByte(20));
        Assert.Equal(2, first.Pools[0].Slots[0].ReadResourcePointer(24).CpuRow);
        first.Pools[0].Slots[0].WriteDword(12, 0x11223344);
        Assert.Equal(16u, first.Memory.Arena.ReadUInt32(0));
        first.Pools[1].Slots[0].WriteDword(12, 0x55667788);
        Assert.Equal(0x55667788u, first.Memory.Arena.ReadUInt32(BoundOffset + 12));
        first.Memory.Arena.Write(BoundOffset + 16, [0x71]);
        Assert.Equal((byte)0x71, first.Pools[1].Slots[0].ReadByte(16));
        Assert.Equal(31u, second.Pools[0].Slots[0].ReadDword(12));
        Assert.Equal(47u, second.Pools[1].Slots[0].ReadDword(12));
        Assert.Equal(31u, live.Pools[0].Slots[0].ReadDword(12));
        Assert.Equal(original, JsonSerializer.Serialize(checkpoint));
    }

    [Fact]
    public void In_memory_restore_keeps_the_existing_shared_kernel_before_binding_admission()
    {
        Saved checkpoint = NativeBattleStorageSnapshots.Capture(Create());
        Saved restored = NativeBattleStorageSnapshots.Restore(checkpoint);
        Assert.Same(restored.Memory.Arena!.Working, restored.Pools[1].RequireOwner().Storage);
        Assert.NotSame(restored.Memory.Arena.Working, restored.Pools[0].RequireOwner().Storage);
        Assert.True(restored.Pools[1].IsBoundToStorage(restored.Memory.Arena, ArenaAddress));
    }

    [Fact]
    public void JSON_declared_binding_is_not_a_physical_binding_until_restore_admission()
    {
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Create()));
        Saved restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<Saved>(json)!);
        Assert.True(restored.Pools[1].RestoredArenaBinding);
        Assert.False(restored.Pools[1].IsBoundToStorage(restored.Memory.Arena!, ArenaAddress));
        NativeBattleEffectTaskMemory.BindArenaBankOwnership(restored.Memory, restored.Pools, restoring: true);
        Assert.True(restored.Pools[1].IsBoundToStorage(restored.Memory.Arena!, ArenaAddress));
    }

    [Fact]
    public void Legacy_JSON_without_receipts_retains_its_existing_task_publication_policy()
    {
        JsonObject document = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Create())))!.AsObject();
        foreach (JsonNode? pool in document["Pools"]!.AsArray())
        {
            pool!.AsObject().Remove("ArenaBinding");
        }
        Saved restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<Saved>(document.ToJsonString())!);
        Assert.Null(restored.Pools[0].RestoredArenaBinding);
        NativeBattleEffectTaskMemory.BindArenaBankOwnership(restored.Memory, restored.Pools, restoring: true);
        Assert.True(restored.Pools[0].IsBoundToStorage(restored.Memory.Arena!, ArenaAddress));
        Assert.Equal(1u, restored.Memory.Arena!.ReadUInt32(0));
        Assert.Equal(31u, restored.Memory.Arena.ReadUInt32(12));
    }

    [Fact]
    public void Subsequent_capture_records_current_binding_instead_of_reusing_restored_intent()
    {
        Saved checkpoint = NativeBattleStorageSnapshots.Capture(Create());
        Saved restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(checkpoint))!);
        Assert.False(restored.Pools[0].RestoredArenaBinding);
        restored.Pools[0].BindArena(restored.Memory.Arena!);
        JsonObject document = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)))!.AsObject();
        Assert.True(document["Pools"]![0]!["ArenaBinding"]!.GetValue<bool>());
    }

    private static Saved Restore(Saved checkpoint, string json, bool serialized)
    {
        Saved restored = NativeBattleStorageSnapshots.Restore(serialized ? JsonSerializer.Deserialize<Saved>(json)! : checkpoint);
        NativeBattleEffectTaskMemory.BindArenaBankOwnership(restored.Memory, restored.Pools, restoring: true);
        return restored;
    }

    private static Saved Create()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(128).Write(0, new byte[128]);
        arena.Write(0, [16, 0, 0, 0, 9, 0, 0, 0]);
        NativeBattleEffectTaskPool independent = NativeBattleEffectTaskPool.Initialize(0x3000000, ArenaAddress, 32, 1);
        (independent, _) = independent.Append(0x6BC040);
        independent.Slots[0].WriteDword(12, 31).WriteByte(20, 0xA7).MarkPayloadUnknown(20, 1).WriteResourceRowReference(24, new(2, null));
        NativeBattleEffectTaskPool bound = NativeBattleEffectTaskPool.Initialize(0x3000010, ArenaAddress + BoundOffset, 32, 1);
        (bound, _) = bound.Append(0x6545F0);
        bound.Slots[0].WriteDword(12, 47);
        bound.BindArena(arena);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = arena }, [independent, bound]);
    }

    private sealed record Saved(NativeBattleEffectMemory Memory, ImmutableArray<NativeBattleEffectTaskPool> Pools);
}
