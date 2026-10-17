namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraPacketArenaTests
{
    [Fact]
    public void Generic_scene_F4_and_G4_write_complete_DWORDs_with_the_actual_arena_bank_and_monotonic_cursor()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        memory = memory with { Arena = memory.Arena!.Write(0x12000, Enumerable.Repeat((byte)0xCC, 60).ToArray()) };
        NativeBattleGeometryPrimitive flat = new(50, [0x05000000, 0x2A123456, 0, 1, 2, 3], [1000, 1001, 1002, 1003]);
        NativeBattleGeometryPrimitive gouraud = new(60, [0x08000000, 0x3A123456, 0, 0xEF010203, 1, 0xAB040506, 2, 0x98070809, 3], [1000, 1001, 1002, 1003]);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0x12000, NativeBattleLogicalOrdering.Clear(), new([new(flat, 0, 0), new(gouraud, 24, 0)], 60), NativeBattleEffectGouraudWrites.FullDword);
        Assert.Equal(0x12000 + 60, written.Cursor);
        Assert.Equal(new byte[] { 0x56, 0x34, 0x12, 0x2A }, written.Memory.Arena!.Read(0x12004, 4));
        Assert.Equal(new byte[] { 0xEF }, written.Memory.Arena.Read(0x12000 + 24 + 15, 1));
        Assert.Equal(new byte[] { 0xAB }, written.Memory.Arena.Read(0x12000 + 24 + 23, 1));
        Assert.Equal(new byte[] { 0x98 }, written.Memory.Arena.Read(0x12000 + 24 + 31, 1));
        Assert.Equal(new[] { 0x12000, 0x12018 }, written.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.All(written.Ordering.Proxies, proxy => Assert.Equal("20DFAB8", proxy.Packet.Bank));
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena.Read(0x12000, 3));
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena.Read(0x12018, 3));
    }
}
