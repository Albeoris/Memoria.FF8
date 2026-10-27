using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCurePacketArenaTests
{
    [Fact]
    public void Wedge_overwrites_native_bytes_preserves_secondary_color_padding_and_marks_only_accepted_link_bytes_unknown()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        memory = memory with { Arena = memory.Arena!.Write(256, Enumerable.Repeat((byte)0xCC, 76).ToArray()) };
        NativeBattleGeometryPrimitive triangle = new(100, [0x06000000, 0x32010203, 0, 0x040506, 1, 0x070809, 2], [1000, 1001, 1002, 0]);
        NativeBattleGeometryPrimitive quad = new(100, [0x08000000, 0x3A040506, 1, 0x040506, 2, 0, 3, 0, 4], [1001, 1002, 1003, 1004]);
        NativeBattleGeometryPrimitive mode = new(100, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
        NativeBattleModulePacketBatch batch = new([new(triangle, 0, 0), new(quad, 28, 0), new(mode, 64, 0)], 76);
        NativeBattleCurePacketWrite write = NativeBattleCurePacketArena.AppendPrimary(memory, new(0, 256, 512, null), NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(332, write.State.PrimaryDrawCursor);
        Assert.Equal(512, write.State.SecondaryDrawCursor);
        Assert.Equal(new[] { 256, 284, 320 }, write.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset).ToArray());
        Assert.All(write.Ordering.Proxies, proxy => Assert.Equal("20DFAB8", proxy.Packet.Bank));
        Assert.Equal(new byte[] { 6 }, write.Memory.Arena!.Read(259, 1));
        Assert.Equal(new byte[] { 0xCC }, write.Memory.Arena.Read(271, 1));
        Assert.Equal(new byte[] { 0xCC }, write.Memory.Arena.Read(279, 1));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, write.Memory.Arena.Read(328, 4));
        Assert.Equal(3, write.Memory.Arena.Read(256, 3).Length);
        Assert.Equal(new byte[] { 3, 2, 1, 0x32 }, write.Memory.Arena.Read(260, 4));
    }

    [Fact]
    public void Extended_sprite_has_its_original_noncontiguous_insertion_order_and_untouched_allocation_padding_after_json_restore()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        memory = memory with { Arena = memory.Arena!.Write(0, Enumerable.Repeat((byte)0xDA, 52).ToArray()) };
        NativeBattleGeometryPrimitive sprite = new(100, [0x09000000, 0x2E808080, 0, 1, 0, 2, 0, 3, 0, 4], [1000, 1000, 1000, 1000]);
        NativeBattleGeometryPrimitive mode = new(100, [0x01000000, 0xE1000220], [0, 0, 0, 0]);
        NativeBattleModulePacketBatch batch = new([new(mode, 40, 0), new(sprite, 0, 0)], 52);
        NativeBattleCureState state = new(0, 0, 65536, null);
        NativeBattleCurePacketWrite write = NativeBattleCurePacketArena.AppendPrimary(memory, state, NativeBattleLogicalOrdering.Clear(), batch);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        NativeBattleCurePacketWrite independent = NativeBattleCurePacketArena.AppendPrimary(restored, state, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(NativeBattleTestSnapshots.Json(write), NativeBattleTestSnapshots.Json(independent));
        Assert.Equal(new[] { 40, 0 }, write.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset).ToArray());
        Assert.Equal(new byte[] { 0xDA, 0xDA }, write.Memory.Arena!.Read(30, 2));
        Assert.Equal(new byte[] { 0xDA, 0xDA }, write.Memory.Arena.Read(38, 2));
        Assert.Equal(new byte[] { 0xDA, 0xDA, 0xDA, 0xDA }, write.Memory.Arena.Read(48, 4));
        Assert.Equal(52, write.State.PrimaryDrawCursor);
    }
}
