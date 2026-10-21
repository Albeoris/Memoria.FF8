using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDevour62QuadTests
{
    [Fact]
    public void Selected_screen_task_emits_foreground_before_background_with_exact_pages_palettes_and_packet_words()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDevour62Quads.Emit(Task());
        Assert.Equal(160, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 40, 80, 120 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        uint[][] expected =
        [
            [0x09000000, 0x2E808080, 0x00100060, 0x3C54D800, 0x00100160, 0x00B6D8FF, 0x00380060, 0x0000FF00, 0x00380160, 0x0000FFFF],
            [0x09000000, 0x2E808080, 0x00100160, 0x3C54D800, 0x00100220, 0x00B8D8C0, 0x00380160, 0x0000FF00, 0x00380220, 0x0000FFC0],
            [0x09000000, 0x2C808080, 0, 0x3C140000, 0x00000080, 0x00B60080, 0x00D80000, 0x0000D800, 0x00D80080, 0x0000D880],
            [0x09000000, 0x2C808080, 0x00000080, 0x3C140000, 0x00000140, 0x00B700C0, 0x00D80080, 0x0000D800, 0x00D80140, 0x0000D8C0]
        ];
        for (int index = 0; index < expected.Length; index++)
        {
            NativeBattleGfGeometryAttempt attempt = batch.Attempts[index];
            Assert.Equal(expected[index], attempt.Primitive!.Words);
            Assert.Equal(5, attempt.Primitive.Bucket);
            Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4);
            Assert.Equal(40, attempt.PacketLength);
            for (int at = 0; at < 40; at++)
            {
                Assert.Equal(at is 30 or 31 or 38 or 39 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[at]);
            }
        }
    }

    [Theory]
    [InlineData(0, 0x2C)]
    [InlineData(1, 0x2E)]
    [InlineData(2, 0x2C)]
    [InlineData(-1, 0x2C)]
    [InlineData(65537, 0x2E)]
    public void Semitransparent_opcode_tests_the_exact_low_WORD_one_and_intensity_uses_its_low_BYTE(int selector, int opcode)
    {
        NativeBattleGfGeometryAttempt packet = Assert.Single(NativeBattleDevour62Quads.EmitQuad(0, 0, 384, 256, 0, 0, 0, 0, 320, 240, 0x1234, selector).Attempts);
        Assert.Equal((byte)opcode, packet.Data[7]);
        Assert.Equal(new byte[] { 0x34, 0x34, 0x34 }, packet.Data.AsSpan().Slice(4, 3).ToArray());
        Assert.NotNull(packet.Primitive);
        Assert.Equal(40, packet.PacketLength);
        Assert.Equal(0x09000000u, Word(packet, 0));
    }

    [Fact]
    public void Signed_WORD_texture_extents_saturate_only_the_upper_sum_and_screen_coordinates_wrap()
    {
        NativeBattleGfGeometryAttempt high = Quad(32767, -32768, 300, 100, 250, 200);
        Assert.Equal((short)32767, Half(high, 8));
        Assert.Equal(unchecked((short)(32767 + 300)), Half(high, 16));
        Assert.Equal((short)-32768, Half(high, 10));
        Assert.Equal(unchecked((short)(-32768 + 100)), Half(high, 26));
        Assert.Equal((byte)255, high.Data[20]);
        Assert.Equal((byte)255, high.Data[36]);
        Assert.Equal((byte)255, high.Data[29]);
        Assert.Equal((byte)255, high.Data[37]);
        NativeBattleGfGeometryAttempt negative = Quad(0, 0, -4, -7, -2, -3);
        Assert.Equal((byte)250, negative.Data[20]);
        Assert.Equal((byte)246, negative.Data[29]);
        NativeBattleGfGeometryAttempt wrappedWidth = Quad(0, 0, 65535, 65535, 0, 0);
        Assert.Equal((byte)255, wrappedWidth.Data[20]);
        Assert.Equal((byte)255, wrappedWidth.Data[29]);
    }

    [Fact]
    public void Current_WORD_offset_wraps_before_both_foreground_positions_and_leaves_background_positions_fixed()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleDevour62Quads.Emit(Task().WriteWord(0x1C, 32760).WriteWord(0x50, 0));
        Assert.Equal(unchecked((short)(32760 + 96)), Half(batch.Attempts[0], 8));
        Assert.Equal(unchecked((short)(32760 + 352)), Half(batch.Attempts[1], 8));
        Assert.Equal((short)0, Half(batch.Attempts[2], 8));
        Assert.Equal((short)128, Half(batch.Attempts[3], 8));
        Assert.All(batch.Attempts, attempt => Assert.Equal(0u, Word(attempt, 4) & 0xFFFFFF));
        Assert.Equal(160, batch.AllocationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Physical_private_packet_stores_keep_padding_and_canonical_nonpacket_owners_across_cold_next(bool unknownPadding)
    {
        const int cursor = 0x6000;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(62);
        NativeBattleEffectArena arena = memory.Arena!.Write(cursor, Enumerable.Repeat((byte)0xA5, 320).ToArray());
        for (int packet = 0; packet < 8; packet++)
        {
            foreach (int padding in new[] { 30, 31, 38, 39 })
            {
                if (unknownPadding)
                {
                    arena = arena.MarkUnknown(cursor + packet * 40 + padding, 1);
                }
            }
        }
        memory = memory with { Arena = arena };
        string geometry = JsonSerializer.Serialize(memory.GeometryRegisters);
        string colors = JsonSerializer.Serialize(memory.ScriptedColorRegisters);
        NativeBattleGfGeometryBatch batch = NativeBattleDevour62Quads.Emit(Task());
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, cursor, NativeBattleLogicalOrdering.Clear(), batch, flags20: 1);
        Assert.Equal(cursor + 160, written.Cursor);
        Assert.Equal(new[] { cursor, cursor + 40, cursor + 80, cursor + 120 }, written.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.All(written.Ordering.Proxies, proxy =>
        {
            Assert.Equal("20DFAB8", proxy.Packet.Bank);
            Assert.Equal((ushort)1, proxy.Flags20);
            Assert.Equal(new[] { 0, 0, 0, 0 }, proxy.VertexPayload4);
        });
        for (int packet = 0; packet < 4; packet++)
        {
            Assert.Equal(new byte[] { 9 }, written.Memory.Arena!.Read(cursor + packet * 40 + 3, 1));
            foreach (int padding in new[] { 30, 31, 38, 39 })
            {
                int at = cursor + packet * 40 + padding;
                if (unknownPadding)
                {
                    Assert.Throws<NotSupportedException>(() => written.Memory.Arena.Read(at, 1));
                }
                else
                {
                    Assert.Equal(new byte[] { 0xA5 }, written.Memory.Arena.Read(at, 1));
                }
            }
        }
        Assert.Equal(geometry, JsonSerializer.Serialize(written.Memory.GeometryRegisters));
        Assert.Equal(colors, JsonSerializer.Serialize(written.Memory.ScriptedColorRegisters));
        NativeBattleEffectPacketWrite restored = JsonSerializer.Deserialize<NativeBattleEffectPacketWrite>(JsonSerializer.Serialize(written))!;
        NativeBattleEffectPacketWrite Next(NativeBattleEffectPacketWrite prior) => NativeBattleEffectPacketArena.Append(prior.Memory, prior.Cursor, prior.Ordering, batch, flags20: 1);
        Assert.Equal(JsonSerializer.Serialize(Next(written)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void Wrong_callback_or_stride_does_not_invent_a_screen_task()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleDevour62Quads.Emit(Task() with { Callback = 0x7E5290 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleDevour62Quads.Emit(Task() with { Payload = new byte[68].ToImmutableArray() }));
        Assert.Throws<NotSupportedException>(() => NativeBattleDevour62Quads.Emit(Task().MarkPayloadUnknown(0x50, 2)));
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 0x7E4F60, new byte[72].ToImmutableArray()).WriteWord(0x1C, 0).WriteWord(0x50, 128);
    private static NativeBattleGfGeometryAttempt Quad(int x, int y, int width, int height, int u, int v) => NativeBattleDevour62Quads.EmitQuad(x, y, 384, 256, width, height, u, v, 320, 240, 128, 0).Attempts[0];
    private static short Half(NativeBattleGfGeometryAttempt packet, int offset) => BinaryPrimitives.ReadInt16LittleEndian(packet.Data.AsSpan().Slice(offset, 2));
    private static uint Word(NativeBattleGfGeometryAttempt packet, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(offset, 4));
}
