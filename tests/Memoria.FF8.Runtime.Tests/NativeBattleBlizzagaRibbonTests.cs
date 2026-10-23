using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzagaRibbonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothNativeRibbonEntriesStoreQuarterDepthAndKeepFarSegmentsBounded(bool sharedEntry)
    {
        ImmutableArray<byte> nodes = Nodes((0, 0, 128), (10, 0, 128), (0, 10, 128), (10, 10, 128)).ToImmutableArray();
        NativeBattleActionMatrix matrix = Matrix with { Z = 65535 };
        NativeBattleBlizzagaRibbonResult result = sharedEntry
            ? NativeBattleBlizzagaRibbon.Emit6F1290(2, nodes, 0, matrix, false, 17, 2, point => NativeBattleModuleProjection.Project45FE10(matrix, point, Projection))
            : NativeBattleBlizzagaRibbon.Emit6D9A40(2, nodes, 0, matrix, Projection);
        Assert.Equal(16383u, U32(result.Nodes, 0x14));
        Assert.Equal(16383u, U32(result.Nodes, 0x1C));
        NativeBattleGeometryPrimitive packet = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(4112, packet.Bucket);
        Assert.Single(NativeBattleLogicalOrdering.Clear().Prepend(packet.Bucket, new("COMMON", 0, 0), packet.Words, 0, packet.VertexPayload4).Packets);
    }

    private static NativeBattleActionMatrix Matrix => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(Matrix, 128, 0, 0);

    [Fact]
    public void Insertion_shifts_only_below_capacity_and_retains_projected_scratch_and_fourth_words()
    {
        byte[] source = Enumerable.Range(0, 30 * 36).Select(index => unchecked((byte)index)).ToArray();
        ImmutableArray<byte> first = [1, 2, 3, 4, 5, 6, 0xAA, 0xBB];
        ImmutableArray<byte> second = [7, 8, 9, 10, 11, 12, 0xCC, 0xDD];
        NativeBattleBlizzagaRibbonNodes inserted = NativeBattleBlizzagaRibbon.Insert6D99D0(2, source.ToImmutableArray(), first, second, 16, 96, 64);
        Assert.Equal(3, inserted.Count);
        Assert.Equal(source.AsSpan(0, 72).ToArray(), inserted.Nodes.AsSpan().Slice(36, 72).ToArray());
        Assert.Equal(first.AddRange(second).ToArray(), inserted.Nodes.AsSpan().Slice(0, 16).ToArray());
        Assert.Equal(source.AsSpan(16, 16).ToArray(), inserted.Nodes.AsSpan().Slice(16, 16).ToArray());
        Assert.Equal(source[35], inserted.Nodes[35]);
        NativeBattleBlizzagaRibbonNodes full = NativeBattleBlizzagaRibbon.Insert6D99D0(30, source.ToImmutableArray(), first, second, 16, 96, 64);
        Assert.Equal(30, full.Count);
        Assert.Equal(source.AsSpan(36).ToArray(), full.Nodes.AsSpan().Slice(36).ToArray());
    }

    [Fact]
    public void Point_math_uses_low_DWORD_products_quarter_radius_and_preserves_fourth_WORD()
    {
        ImmutableArray<byte> seed = [0, 0, 0, 0, 0, 0, 0xA5, 0x5A];
        ImmutableArray<byte> point = NativeBattleBlizzagaRibbon.Point6D9CF0(seed, 0, 100, 4096);
        Assert.Equal(1024, I16(point, 0));
        Assert.Equal(0, I16(point, 2));
        Assert.Equal(-75, I16(point, 4));
        Assert.Equal(0x5AA5, I16(point, 6));
        Assert.Equal(-1, I16(NativeBattleBlizzagaRibbon.Point6D9CF0(seed, 0, 0, int.MaxValue), 0));
    }

    [Fact]
    public void Brightness_wraps_signed_velocity_and_upper_BYTE_then_trims_only_one_tail_node()
    {
        byte[] nodes = new byte[30 * 36];
        nodes[0x20] = 250;
        nodes[0x21] = 120;
        NativeBattleBlizzagaRibbonResult wrapped = Emit(1, nodes, 0);
        Assert.Equal(9, wrapped.Nodes[0x20]);
        nodes[0x20] = 1;
        nodes[0x21] = 128;
        NativeBattleBlizzagaRibbonResult velocityWrapped = Emit(1, nodes, -8);
        Assert.Equal(120, velocityWrapped.Nodes[0x21]);
        Assert.Equal(16, velocityWrapped.Nodes[0x20]);
        nodes[0x20] = 0;
        nodes[0x21] = 0;
        NativeBattleBlizzagaRibbonResult trimmed = Emit(4, nodes, -8);
        Assert.Equal(3, trimmed.Count);
        Assert.Equal(248, trimmed.Nodes[3 * 36 + 0x21]);
        Assert.Equal(0, trimmed.Packets.AllocationBytes);
    }

    [Fact]
    public void Twisted_segment_swaps_only_output_SXY_and_V_while_preserving_original_proxy_depth_order()
    {
        byte[] nodes = Nodes((0, 0, 128), (10, 0, 128), (0, 10, 128), (0, -10, 256));
        NativeBattleBlizzagaRibbonResult result = Emit(2, nodes, 0);
        NativeBattleGeometryPrimitive packet = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(52, result.Packets.AllocationBytes);
        Assert.Equal(0x0C000000u, packet.Words[0]);
        Assert.Equal(0x3E101010u, packet.Words[1]);
        Assert.Equal(0x202020u, packet.Words[7]);
        Assert.Equal(U32(result.Nodes, 36 + 0x18), packet.Words[8]);
        Assert.Equal(U32(result.Nodes, 36 + 0x10), packet.Words[11]);
        Assert.Equal(0x3E144040u, packet.Words[3]);
        Assert.Equal(0x00B87F40u, packet.Words[6]);
        Assert.Equal(0x7F5Fu, packet.Words[9]);
        Assert.Equal(0x405Fu, packet.Words[12]);
        Assert.Equal(new[] { 196, 196, 196, 324 }, packet.VertexPayload4.ToArray());
        Assert.Equal(27, packet.Bucket);
        Assert.All(result.Packets.Attempts[0].WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Backfaces_screen_clip_and_far_depth_remain_admitted_in_quarter_depth_units()
    {
        byte[] backwards = Nodes((0, 0, 128), (-10, 0, 128), (0, 10, 128), (-10, 10, 128));
        Assert.Equal(52, Emit(2, backwards, 0).Packets.AllocationBytes);
        byte[] outside = Nodes((20000, 0, 128), (20001, 0, 128), (20000, 10, 128), (20001, 10, 128));
        Assert.Equal(52, Emit(2, outside, 0).Packets.AllocationBytes);
        NativeBattleActionMatrix far = Matrix with { Z = 32768 };
        NativeBattleBlizzagaRibbonResult rejected = NativeBattleBlizzagaRibbon.Emit6D9A40(2, backwards.ToImmutableArray(), -8, far, Projection);
        Assert.Equal(52, rejected.Packets.AllocationBytes);
        Assert.Equal(8224u, U32(rejected.Nodes, 0x14));
        Assert.Equal(15, rejected.Nodes[0x20]);
    }

    [Fact]
    public void Retained_nodes_roundtrip_to_an_identical_next_mutation_projection_and_packet_batch()
    {
        NativeBattleBlizzagaRibbonResult first = Emit(2, Nodes((0, 0, 128), (10, 0, 128), (0, 10, 128), (10, 10, 128)), -4);
        string json = JsonSerializer.Serialize(first);
        NativeBattleBlizzagaRibbonResult restored = JsonSerializer.Deserialize<NativeBattleBlizzagaRibbonResult>(json)!;
        NativeBattleBlizzagaRibbonResult next = NativeBattleBlizzagaRibbon.Emit6D9A40(first.Count, first.Nodes, -4, Matrix, Projection);
        NativeBattleBlizzagaRibbonResult resumed = NativeBattleBlizzagaRibbon.Emit6D9A40(restored.Count, restored.Nodes, -4, Matrix, Projection);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(resumed));
        Assert.Equal(json, JsonSerializer.Serialize(first));
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaRibbon.Emit6D9A40(31, first.Nodes, -4, Matrix, Projection));
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaRibbon.Emit6D9A40(2, first.Nodes.RemoveAt(0), -4, Matrix, Projection));
    }

    private static NativeBattleBlizzagaRibbonResult Emit(int count, byte[] nodes, sbyte delta) => NativeBattleBlizzagaRibbon.Emit6D9A40(count, nodes.ToImmutableArray(), delta, Matrix, Projection);
    private static short I16(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(offset));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));

    private static byte[] Nodes(params (short X, short Y, short Z)[] points)
    {
        byte[] nodes = new byte[30 * 36];
        for (int index = 0; index < points.Length; index++)
        {
            int offset = index / 2 * 36 + index % 2 * 8;
            BinaryPrimitives.WriteInt16LittleEndian(nodes.AsSpan(offset), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(nodes.AsSpan(offset + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(nodes.AsSpan(offset + 4), points[index].Z);
        }
        nodes[0x20] = 16;
        nodes[0x22] = 64;
        nodes[36 + 0x20] = 32;
        return nodes;
    }
}
