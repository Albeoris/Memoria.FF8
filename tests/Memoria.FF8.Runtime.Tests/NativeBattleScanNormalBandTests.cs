using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanNormalBandTests
{
    [Fact]
    public void Timeline_initializes_without_drawing_caps_fade_and_draws_zero_before_parent_completion()
    {
        var fixture = Fixture();
        List<NativeBattleModulePacketBatch> draws = [];
        NativeBattleScanNormalBandFrame Draw(NativeBattleScanNormalBandFrame frame, NativeBattleModulePacketBatch batch)
        {
            draws.Add(batch);
            return frame;
        }
        NativeBattleScanNormalBandFrame frame = NativeBattleScanNormalBand.Advance84EFF0(fixture.Frame, fixture.Address, Draw).Frame;
        Assert.Empty(draws);
        for (int index = 0; index < 16; index++)
        {
            frame = NativeBattleScanNormalBand.Advance84EFF0(frame, fixture.Address, Draw).Frame;
        }
        Assert.Equal((ushort)128, Task(frame, fixture.Address).ReadWord(0x6E));
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        Assert.Equal(0x2C808080u, draws[^1].Primitives[0].Primitive.Words[1]);
        frame = NativeBattleScanNormalBand.Advance84EFF0(frame with { Ready269D938 = 2 }, fixture.Address, Draw).Frame;
        Assert.Equal((byte)2, Task(frame, fixture.Address).ReadByte(0x29));
        frame = NativeBattleScanNormalBand.Advance84EFF0(frame with { Ready269D938 = 1 }, fixture.Address, Draw).Frame;
        NativeBattleScanNormalBandStep step = new(frame, 0);
        for (int index = 0; index < 16; index++)
        {
            step = NativeBattleScanNormalBand.Advance84EFF0(step.Frame, fixture.Address, Draw);
        }
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)0, Task(step.Frame, fixture.Address).ReadWord(0x6E));
        Assert.Equal(0x2C000000u, draws[^1].Primitives[0].Primitive.Words[1]);
        Assert.Equal((byte)0, Task(step.Frame, 0x269AAE0).ReadByte(0x28));
        Assert.Equal((ushort)1, Task(step.Frame, fixture.Address).Flags);
        Assert.Equal(34, draws.Count);
    }

    [Theory]
    [InlineData(63, -1)]
    [InlineData(64, -64)]
    [InlineData(0xFFFF, -1)]
    public void Grid_keeps_row_major_packet_order_unsigned_intensity_scroll_wrap_and_literal_materials(ushort counter, short firstY)
    {
        NativeBattleModulePacketBatch batch = NativeBattleScanNormalBand.Draw84F0B0(counter, 255);
        Assert.Equal(600, batch.AllocationBytes);
        Assert.Equal(15, batch.Primitives.Length);
        for (int index = 0; index < 15; index++)
        {
            NativeBattleAllocatedPrimitive allocated = batch.Primitives[index];
            var words = allocated.Primitive.Words;
            Assert.Equal(index * 40, allocated.ByteOffset);
            Assert.Equal((ushort)1, allocated.Flags20);
            Assert.Equal(4381, allocated.Primitive.Bucket);
            Assert.Equal(new int[] { 0, 0, 0, 0 }, allocated.Primitive.VertexPayload4);
            Assert.Equal(0x09000000u, words[0]);
            Assert.Equal(0x2CFFFFFFu, words[1]);
            Assert.Equal((ushort)(index % 3 * 128), (ushort)words[2]);
            Assert.Equal(unchecked((ushort)(firstY + index / 3 * 64)), (ushort)(words[2] >> 16));
            Assert.Equal(0x3C940080u, words[3]);
            Assert.Equal(0x00BA0000u, words[5]);
            Assert.Equal(0x4080u, words[7]);
            Assert.Equal(0x4000u, words[9]);
        }
    }

    [Fact]
    public void Actual_module_arena_writer_preserves_unwritten_UV_high_words_and_advances_private_cursor_six_hundred_bytes()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(40);
        memory = memory with { Arena = memory.Arena!.Write(0x4000, Enumerable.Repeat((byte)0xA5, 600).ToArray()) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0x4000, NativeBattleLogicalOrdering.Clear(), NativeBattleScanNormalBand.Draw84F0B0(0, 128), NativeBattleEffectGouraudWrites.FullDword);
        Assert.Equal(0x4000 + 600, written.Cursor);
        Assert.Equal(15, written.Ordering.Proxies.Length);
        for (int index = 0; index < 15; index++)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena!.Read(0x4000 + index * 40 + 30, 2));
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena.Read(0x4000 + index * 40 + 38, 2));
        }
    }

    private static NativeBattleModulePoolTask Task(NativeBattleScanNormalBandFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Scan.Effect.Pools).Read(address);

    private static (NativeBattleScanNormalBandFrame Frame, uint Address) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 50, 40, null));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(40, 39, new(40, 0x84D030, 0x84D010), new(2, 1, 16, 0, 0, 0), null);
        NativeBattleScanFrame scan = NativeBattleScanController.Initialize84D030(NativeBattleEffectMemory.FromColdProcess(), effect, packets);
        NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(scan.Effect.Pools).Append8DC540(2, 0x84EFF0, 0x78, 0x269AAE0, (_, _, _) => new(2, 4));
        scan = scan with { Effect = scan.Effect with { Pools = child.Hierarchy.Pools } };
        return (new(scan, 0), child.Address);
    }
}
