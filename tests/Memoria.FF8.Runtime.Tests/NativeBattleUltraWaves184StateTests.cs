using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleUltraWaves184Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWaves184StateTests
{
    [Fact]
    public void Loader_banks_and_unknown_state_remain_distinct()
    {
        NativeBattleUltraWaves184State cold = NativeBattleUltraWaves184State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadCursor());
        Assert.Equal(new byte[24], cold.Particles.Read(0, 24));
        Assert.Throws<NotSupportedException>(() => cold.CapturePackets.Read(0, 48));
        NativeBattleUltraWaves184State unknown = NativeBattleUltraWaves184State.FromUnknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.ReadCursor());
        Assert.Throws<NotSupportedException>(() => unknown.Particles.Read(0, 2));
    }

    [Theory]
    [InlineData(RootBank)]
    [InlineData(RootDescriptor)]
    [InlineData(ChildBank)]
    [InlineData(ChildDescriptor)]
    [InlineData(ParticlesAddress)]
    [InlineData(QuadsAddress)]
    [InlineData(GridAddress)]
    [InlineData(CursorAddress)]
    public void Sparse_globals_do_not_duplicate_other_physical_owners(uint address)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleUltraWaves184State.FromColdProcess().WriteDword(address, 0));
    }

    [Fact]
    public void Common_texture_overwrites_the_single_cursor_and_actual_neighbor_tail()
    {
        byte[] bytes = Enumerable.Range(0, PacketPhysicalLength).Select(index => unchecked((byte)(index * 17))).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(PacketDrawingLength, 4), 0x10);
        NativeBattleUltraWaves184State state = NativeBattleUltraWaves184State.FromColdProcess().WriteCursor(PacketAddress + PacketBankLength).CopyCommonTexture(bytes);
        state.Validate();
        Assert.Equal(0x10u, state.ReadCursor());
        Assert.Equal(bytes, state.Packets.Read(0, bytes.Length));
        string json = JsonSerializer.Serialize(state);
        NativeBattleUltraWaves184State restored = JsonSerializer.Deserialize<NativeBattleUltraWaves184State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => state.CopyCommonTexture(bytes.AsSpan(0, PacketDrawingLength)));
    }
}
