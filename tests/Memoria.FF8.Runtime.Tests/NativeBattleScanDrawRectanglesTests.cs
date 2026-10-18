using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScanDrawRectanglesTests
{
    [Theory]
    [InlineData(10, 10, 4, 4, true)]
    [InlineData(20, 10, 0, 4, false)]
    [InlineData(10, 20, 4, 0, false)]
    public void Intersection_is_signed_half_open_without_inclusive_edge_pixels(short x, short y, short width, short height, bool visible)
    {
        NativeBattleScanIntersection intersection = NativeBattleScanDrawRectangles.Intersect4B6350(new(10, 10, 10, 10), new(x, y, 4, 4));
        Assert.Equal(new NativeBattleScanRect(x, y, width, height), intersection.Rectangle);
        Assert.Equal(visible, intersection.Visible);
    }

    [Fact]
    public void Backend_clipping_publishes_current_pointer_and_keeps_originalXY_and_each_actual_area_snapshot()
    {
        NativeBattleScanByteOwner backend = Unknown(64).Write(8, new NativeBattleScanRect(5, 5, 20, 20).ToBytes()).Write(16, new NativeBattleScanRect(-5, -5, 2, 1).ToBytes());
        List<uint> publications = [];
        int reads = 0;
        NativeBattleScanBackendClip result = NativeBattleScanDrawRectangles.ClipBackend4B6400(backend, Unknown(24), 0x1234, () =>
        {
            Assert.Equal(new uint[] { 0x124C }, publications);
            return reads++ == 0 ? new(10, 10, 15, 15) : new(100, 200, 20, 30);
        }, publications.Add);
        Assert.Equal(2, reads);
        Assert.Equal(new uint[] { 0x124C, 0x1234 }, publications);
        Assert.Equal(0x1234u, result.ContextPointer);
        Assert.True(result.Visible);
        Assert.Equal(new NativeBattleScanRect(15, 15, 10, 10).ToBytes(), result.Backend.Bytes.AsSpan(0x18, 8).ToArray());
        Assert.Equal((ushort)15, result.Backend.ReadWord(0x20));
        Assert.Equal((ushort)15, result.Backend.ReadWord(0x22));
        Assert.Equal(new NativeBattleScanRect(100, 200, 2, 1).ToBytes(), result.Backend.Bytes.AsSpan(0x24, 8).ToArray());
        Assert.Equal((ushort)95, result.Backend.ReadWord(0x2C));
        Assert.Equal((ushort)195, result.Backend.ReadWord(0x2E));
        Assert.Equal(new NativeBattleScanRect(100, 200, 20, 30).ToBytes(), result.ContextScratch.Bytes.AsSpan(12, 8).ToArray());
        Assert.Equal((ushort)100, result.ContextScratch.ReadWord(20));
        Assert.Equal((ushort)200, result.ContextScratch.ReadWord(22));
        Assert.False(result.Backend.Known[0x30]);
        Assert.Equal(backend.Bytes.AsSpan(8, 16).ToArray(), result.Backend.Bytes.AsSpan(8, 16).ToArray());
    }

    [Fact]
    public void Nonpositive_outer_is_invisible_but_still_wrapsXY_and_minimum_clamps_its_snapshot()
    {
        NativeBattleScanByteOwner backend = Unknown(64).Write(8, new NativeBattleScanRect(32767, 32767, -3, 0).ToBytes()).Write(16, new NativeBattleScanRect(0, 0, 0, 0).ToBytes());
        NativeBattleScanBackendClip result = NativeBattleScanDrawRectangles.ClipBackend4B6400(backend, Unknown(24), 0, () => new(1, 2, 20, 30), _ => { });
        Assert.False(result.Visible);
        Assert.Equal(new NativeBattleScanRect(-32768, -32767, 2, 1).ToBytes(), result.Backend.Bytes.AsSpan(0x18, 8).ToArray());
    }

    [Fact]
    public void Fill_submits_reset_then_two_real_window_commands_using_unsigned_even_width_and_same_scratch()
    {
        List<(NativeBattleScanRectangleSubmission Kind, NativeBattleScanByteOwner Packet)> packets = [];
        List<string> calls = [];
        NativeBattleScanRectangleServices services = Services(packets, calls);
        List<int> commands = [];
        NativeBattleScanRectangleFrame result = NativeBattleScanDrawRectangles.EmitFill4B6A80(new(Unknown(32), 0x4321, 0xABCD), new(-2, -3, 137, 28), 0xFF112233, 0, services, index =>
        {
            commands.Add(index);
            return index == 0 ? 0xE20303D0u : 0xE20343D0u;
        });
        Assert.Equal(new[] { 0, 1 }, commands);
        Assert.Equal(new[] { "begin", "submit", "submit", "submit", "end" }, calls);
        Assert.Equal(3, packets.Count);
        Assert.Equal(NativeBattleScanRectangleSubmission.Reset49D680, packets[0].Kind);
        Assert.Equal(12, packets[0].Packet.Known.Count(value => value));
        Assert.Equal(0xE20303D0u, packets[1].Packet.ReadDword(8));
        Assert.Equal((ushort)128, packets[1].Packet.ReadWord(28));
        Assert.Equal(unchecked((ushort)-2), packets[1].Packet.ReadWord(20));
        Assert.Equal(0x66112233u, packets[1].Packet.ReadDword(16));
        Assert.Equal(0xE20343D0u, packets[2].Packet.ReadDword(8));
        Assert.Equal((ushort)8, packets[2].Packet.ReadWord(28));
        Assert.Equal((ushort)126, packets[2].Packet.ReadWord(20));
        Assert.Equal((ushort)0x3850, packets[2].Packet.ReadWord(26));
        Assert.Equal(0x4321u, result.ScratchAddress);
        Assert.Equal(0xABCDu, result.Tail);
    }

    [Fact]
    public void Border_submits_every_reused_snapshot_in_order_and_only_final_bottom_uses_required_depth_offset()
    {
        List<(NativeBattleScanRectangleSubmission Kind, NativeBattleScanByteOwner Packet)> packets = [];
        List<string> calls = [];
        NativeBattleScanRectangleServices services = Services(packets, calls);
        List<float> offsets = [];
        float currentOffset = 0;
        List<float> submittedOffsets = [];
        services = services with { Submit = (kind, tail, address, packet) =>
        {
            packets.Add((kind, packet));
            submittedOffsets.Add(currentOffset);
            return tail;
        } };
        NativeBattleScanDrawRectangles.EmitBorder4B6530(new(Unknown(32), 0x4321, 0xABCD), new(170, 30, 136, 28), 0x64808080, 3, services, () => 1, value =>
        {
            currentOffset = value;
            offsets.Add(value);
        });
        Assert.Equal(11, packets.Count);
        Assert.Equal(new[] { -1f, 0f }, offsets);
        Assert.All(submittedOffsets.Take(10), offset => Assert.Equal(0f, offset));
        Assert.Equal(-1f, submittedOffsets[10]);
        Assert.Equal(12, packets[0].Packet.Known.Count(value => value));
        Assert.Equal(24, packets[1].Packet.Known.Count(value => value));
        Assert.Equal((ushort)170, packets[1].Packet.ReadWord(12));
        Assert.Equal((ushort)30, packets[1].Packet.ReadWord(14));
        Assert.Equal((ushort)0x10, packets[1].Packet.ReadWord(16));
        Assert.Equal((ushort)50, packets[2].Packet.ReadWord(22));
        Assert.Equal(0x38101010u, packets[2].Packet.ReadDword(24));
        Assert.Equal(0xE2008BFFu, packets[4].Packet.ReadDword(8));
        Assert.Equal((ushort)12, packets[4].Packet.ReadWord(30));
        Assert.Equal((ushort)298, packets[5].Packet.ReadWord(12));
        Assert.Equal(0xE20093FFu, packets[8].Packet.ReadDword(8));
        Assert.Equal((ushort)178, packets[9].Packet.ReadWord(20));
        Assert.Equal((ushort)120, packets[9].Packet.ReadWord(28));
        Assert.Equal(0xE2010FFFu, packets[10].Packet.ReadDword(8));
        Assert.Equal((ushort)50, packets[10].Packet.ReadWord(22));
    }

    [Fact]
    public void Small_border_crops_corners_without_reading_depth_flag_and_wide_border_emits_the_actual_single240_pair()
    {
        List<(NativeBattleScanRectangleSubmission Kind, NativeBattleScanByteOwner Packet)> packets = [];
        NativeBattleScanRectangleServices services = Services(packets, []);
        NativeBattleScanDrawRectangles.EmitBorder4B6530(new(Unknown(32), 0x4321, 0xABCD), new(1, 2, 6, 10), 0, 3, services, () => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        Assert.Equal(7, packets.Count);
        Assert.Equal((ushort)3, packets[1].Packet.ReadWord(20));
        Assert.Equal((ushort)5, packets[1].Packet.ReadWord(22));
        Assert.Equal((ushort)0x1310, packets[2].Packet.ReadWord(24));
        Assert.Equal((ushort)0x25, packets[4].Packet.ReadWord(16));
        Assert.Equal((ushort)0x1325, packets[5].Packet.ReadWord(24));
        packets.Clear();
        NativeBattleScanDrawRectangles.EmitBorder4B6530(new(Unknown(32), 0x4321, 0xABCD), new(1, 2, 300, 28), 0, 3, services, () => 0, _ => { });
        Assert.Equal(13, packets.Count);
        Assert.Equal((ushort)240, packets[9].Packet.ReadWord(28));
        Assert.Equal((ushort)240, packets[10].Packet.ReadWord(28));
        Assert.Equal((ushort)44, packets[11].Packet.ReadWord(28));
        Assert.Equal((ushort)249, packets[11].Packet.ReadWord(20));
    }

    private static NativeBattleScanRectangleServices Services(List<(NativeBattleScanRectangleSubmission Kind, NativeBattleScanByteOwner Packet)> packets, List<string> calls)
    {
        return new(() => calls.Add("begin"), () => calls.Add("end"), (kind, tail, address, packet) =>
        {
            calls.Add("submit");
            Assert.Equal(0x4321u, address);
            Assert.Equal(0xABCDu, tail);
            packets.Add((kind, packet));
            return tail;
        });
    }

    private static NativeBattleScanByteOwner Unknown(int length) => new(new byte[length].ToImmutableArray(), new bool[length].ToImmutableArray());
}
