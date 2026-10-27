using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicOverlayTests
{
    [Theory]
    [InlineData(0x004D)]
    [InlineData(0xFE4D)]
    public void Registration_reads_no_operand_clears_only_selected_fields_and_registers_draw4(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, 0, 0).WriteTemporaryWord(0x14A, instruction);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x80001000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x18, 0xA5FF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x48, 0xFFFF);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1000, 8) };
        NativeBattleWaterExecutionFrame result = NativeBattleWaterPresentationCommands.Execute(frame, new(0x1000, 2, 0x80001000), new(0x2000, 0x400, 4, 0x2000), 0, new(null!, null!, null!));
        Assert.Equal((ushort)0xA500, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x18));
        Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(result, 0, 0x48));
        Assert.Equal((ushort)4, (ushort)(WaterSchedulerFixture.ReadSlotWord(result, 0, 0x1C) & 0xFF));
        Assert.Equal(0x80001002u, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Equal((byte)0, result.Core.Read(0x2797554, 1)[0]);
    }

    [Fact]
    public void Zero_color_writes_only_header_length_byte_and_keeps_cursor_and_unknown_packet_tail()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, 0, 0);
        NativeBattleCinematicOverlayStep step = NativeBattleCinematicOverlay.Draw(frame, _ => throw new InvalidOperationException("Zero color must not insert into the ordering table."));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(new byte[] { 0, 0, 0, 5 }, attempt.Data.ToArray());
        Assert.Equal(new[] { NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known }, attempt.WriteMask.ToArray());
        Assert.Equal(0, step.Packets.AllocationBytes);
        Assert.Equal(0x400u, WaterSchedulerFixture.ReadCoreDword(step.Frame, 0x2797690 + 0x7C));
        Assert.Equal(0x20u, step.Frame.ReadTemporaryDword(0xF0));
    }

    [Theory]
    [InlineData(1, 2, 3, 0x00030201u, 0x20u)]
    [InlineData(-1, 2, 3, 0x00030201u, 0x40u)]
    [InlineData(1, -2, 3, 0x00030201u, 0x40u)]
    [InlineData(1, 2, -3, 0x00030201u, 0x40u)]
    [InlineData(-32768, 256, 32767, 0xFFFFFFu, 0x40u)]
    public void Overlay_preserves_signed_clamp_screen_extent_packet_order_and_distinct_depth_buckets(short red, short green, short blue, uint color, uint mode)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(red, green, blue);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0xDE, 0x20);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xC0, 0x10003);
        List<uint> addresses = [];
        NativeBattleCinematicOverlayStep step = NativeBattleCinematicOverlay.Draw(frame, address =>
        {
            addresses.Add(address);
            return addresses.Count;
        });
        Assert.Equal(new uint[] { 0x40030, 0x24 }, addresses);
        Assert.Equal(36, step.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt quad = step.Packets.Attempts[1];
        NativeBattleGfGeometryAttempt drawMode = step.Packets.Attempts[2];
        Assert.Equal(new uint[] { 0x05000000, 0x2A000000 | color, 0, 320, 0x01000000, 0x01000140 }, quad.Primitive!.Words.ToArray());
        Assert.Equal(new uint[] { 0x02000000, 0xE1000000 | mode, 0 }, drawMode.Primitive!.Words.ToArray());
        Assert.Equal(1, quad.Primitive.Bucket);
        Assert.Equal(2, drawMode.Primitive.Bucket);
        Assert.Equal(24, drawMode.ByteOffsetRelative);
        Assert.Equal(0x424u, WaterSchedulerFixture.ReadCoreDword(step.Frame, 0x2797690 + 0x7C));
        Assert.Equal(mode, step.Frame.ReadTemporaryDword(0xF0));
        string json = NativeBattleTestSnapshots.Json(step);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleCinematicOverlayStep>(json)!));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)64)]
    public void Physical_overlay_stores_preserve_unknown_links_and_unused_tail_through_snapshot(short red)
    {
        NativeBattleCinematicOverlayStep overlay = NativeBattleCinematicOverlay.Draw(Fixture(red, 0, 0), address => checked((int)(address / 4)));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(201);
        memory = memory with { Arena = memory.Arena!.MarkUnknown(128, 3).Write(132, Enumerable.Repeat((byte)0xA5, 48).ToArray()) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), overlay.Packets);
        Assert.Equal(128 + overlay.Packets.AllocationBytes, written.Cursor);
        Assert.Equal((byte)5, written.Memory.Arena!.Read(131, 1)[0]);
        if (red == 0)
        {
            Assert.Throws<NotSupportedException>(() => written.Memory.Arena.Read(128, 3));
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 48), written.Memory.Arena.Read(132, 48));
            Assert.Empty(written.Ordering.Packets);
        }
        else
        {
            Assert.Equal(2, written.Ordering.Packets.Length);
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 16), written.Memory.Arena.Read(164, 16));
        }
        string json = NativeBattleTestSnapshots.Json(written);
        Assert.Equal(json, NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectPacketWrite>(json)!));
    }

    private static NativeBattleWaterExecutionFrame Fixture(short red, short green, short blue)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973EC, 0x2797690);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x400);
        frame = frame.WriteTemporaryDword(0x14C, 0x24);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)red));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, unchecked((ushort)green));
        return WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)blue));
    }
}
