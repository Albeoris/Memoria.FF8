using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectHeapLoadTests
{
    [Fact]
    public void HeapFilesRetainIndependentIdentitiesWithoutConsumingArenaOrImplicitlyPublishingTextures()
    {
        NativeBattleEffectMemory original = NativeBattleEffectMemory.FromColdProcess().BeginLoad(198).LoadIntoArena([0xA1, 0xB2, 0xC3]);
        NativeBattleEffectHeapAllocation retainedTail = new("retained-tail", [9]);
        original = original with { Allocations = original.Allocations.SetItem(12, retainedTail) };
        NativeBattleEffectHeapLoad first = original.LoadHeap5718E0(new("first.bin", [1, 2, 3, 4]));
        NativeBattleEffectHeapLoad second = first.Memory.LoadHeap5718E0(new("second.bin", [5, 6]));
        Assert.Equal(0, first.AllocationIndex);
        Assert.Equal(1, second.AllocationIndex);
        Assert.Equal(2, second.Memory.AllocationCount);
        Assert.Equal(0, original.AllocationCount);
        Assert.Same(original.Arena, second.Memory.Arena);
        Assert.Equal(3, second.Memory.Cursor);
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3, 0 }, second.Memory.Arena!.Read(0, 4));
        Assert.Equal("first.bin", second.Memory.Allocations[0]!.Identity);
        Assert.Equal(new byte[] { 5, 6 }, second.Memory.Allocations[1]!.Data.ToArray());
        Assert.Equal(0, second.Memory.Textures!.Count);
        Assert.Null(second.Memory.Textures.Animation);
        NativeBattleEffectMemory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second.Memory)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second.Memory)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        NativeBattleEffectMemory released = restored.BeginLoad(199);
        Assert.Equal(0, released.AllocationCount);
        Assert.Null(released.Allocations[0]);
        Assert.Null(released.Allocations[1]);
        Assert.Equal("retained-tail", released.Allocations[12]!.Identity);
        Assert.Equal(new byte[4], released.Arena!.Read(0, 4));
        Assert.Equal(0, released.Cursor);
    }

    [Fact]
    public void PhysicalSlot255SucceedsAndSlot256FailsWithoutInventingAnAddressOrAnArena()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        for (int index = 0; index < 256; index++)
        {
            NativeBattleEffectHeapLoad loaded = memory.LoadHeap5718E0(new($"file-{index}", [(byte)index]));
            Assert.Equal(index, loaded.AllocationIndex);
            memory = loaded.Memory;
        }
        Assert.Null(memory.Arena);
        Assert.Equal(0, memory.Cursor);
        Assert.Equal(256, memory.AllocationCount);
        Assert.Equal(new byte[] { 255 }, memory.Allocations[255]!.Data.ToArray());
        string before = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory));
        Assert.Throws<NotSupportedException>(() => memory.LoadHeap5718E0(new("overflow", [0])));
        Assert.Equal(before, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(memory)));
        NativeBattleEffectMemory freed = memory.BeginLoad(198);
        Assert.All(freed.Allocations, allocation => Assert.Null(allocation));
        Assert.Equal(0, freed.LoadHeap5718E0(new("reused", [7])).AllocationIndex);
        Assert.Throws<InvalidDataException>(() => freed.LoadHeap5718E0(new("", [7])));
        Assert.Throws<InvalidDataException>(() => freed.LoadHeap5718E0(new("unknown", default)));
        Assert.Throws<InvalidDataException>(() => (freed with { Allocations = ImmutableArray<NativeBattleEffectHeapAllocation?>.Empty }).LoadHeap5718E0(new("file", [7])));
    }
}
