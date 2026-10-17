using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGeometryPacketTests
{
    private static readonly NativeBattleProjectedVertex[] Vertices = [new(0, 100), new(80, 80), new(80u << 16, 120), new(80u | 80u << 16, 90)];

    [Fact]
    public void FT3_uses_min_unsigned_depth_and_keeps_bias_out_of_bucket_selection()
    {
        NativeBattleGeometryPacket actual = NativeBattleGeometryPackets.Emit50FDF0(Polygon(false), Vertices, false, 0x808080, 17, 4, 0x2000000)!;
        Assert.Equal(22, actual.Bucket);
        Assert.Equal(new int[] { 102, 82, 122, 0 }, actual.VertexPayload4);
        Assert.Equal(new uint[] { 0x7000000, 0x24808080, 0, 0x12340201, 80, 0x00400403, 80u << 16, 0x0605 }, actual.Packet.Words);
        NativeBattlePrimitiveOrdering ordering = NativeBattlePrimitiveOrdering.InitializeBattle(0).PrependProjected45C870(actual.Bucket, actual.Packet, actual.VertexPayload4);
        Assert.Equal((ushort)0, ordering.Proxies[0].Flags20);
        Assert.Equal(actual.VertexPayload4, ordering.Proxies[0].VertexPayload4);
        NativeBattlePrimitiveOrdering restored = JsonSerializer.Deserialize<NativeBattlePrimitiveOrdering>(JsonSerializer.Serialize(ordering))!;
        Assert.Equal(actual.VertexPayload4, restored.Proxies[0].VertexPayload4);
    }

    [Fact]
    public void FT4_copies_the_original_four_UVs_and_the_native_page_flags_force_semitransparency()
    {
        byte[] polygon = Polygon(true);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(14), 0x240);
        NativeBattleGeometryPacket actual = NativeBattleGeometryPackets.Emit50FDF0(polygon, Vertices, true, 0x808080, 17, 4, 0x2000000)!;
        Assert.Equal(0x2E808080u, actual.Packet.Words[1]);
        Assert.Equal(0x0605u, actual.Packet.Words[7]);
        Assert.Equal(0x0807u, actual.Packet.Words[9]);
        Assert.Equal(new int[] { 102, 82, 122, 92 }, actual.VertexPayload4);
    }

    [Theory]
    [InlineData(0x20000u, 0u, 0u)]
    [InlineData(0x1000000u, 0x1000000u, 0x1000000u)]
    public void Original_near_and_shared_screen_clip_flags_suppress_packet_emission(uint a, uint b, uint c)
    {
        NativeBattleProjectedVertex[] vertices = Vertices.ToArray();
        vertices[0] = vertices[0] with { DepthAndClip = 100 | a };
        vertices[1] = vertices[1] with { DepthAndClip = 80 | b };
        vertices[2] = vertices[2] with { DepthAndClip = 120 | c };
        Assert.Null(NativeBattleGeometryPackets.Emit50FDF0(Polygon(false), vertices, false, 0x808080, 17, 4, 0x2000000));
    }

    [Fact]
    public void Colored_groups_keep_the_original_command_and_require_the_actual_depth_cue_owner()
    {
        byte[] polygon = new byte[20];
        Polygon(false).CopyTo(polygon, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(16), 0x26808080);
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometryPackets.Emit50FDF0(polygon, Vertices, false, 0, 17, 4, 0x2000000));
        Assert.Throws<NotSupportedException>(() => NativeBattleGeometryPackets.Emit50FDF0(polygon, Vertices, false, 0, 17, 4, 0x2000000, 1));
        Assert.Equal(0x26808080u, NativeBattleGeometryPackets.Emit50FDF0(polygon, Vertices, false, 0, 17, 4, 0x2000000, 0)!.Packet.Words[1]);
    }

    [Theory]
    [InlineData(false, 2048, 0u, 0x26404040u)]
    [InlineData(true, 2048, 0u, 0x26404040u)]
    [InlineData(false, 4096, 0x002040C0u, 0x262040C0u)]
    [InlineData(true, -4096, 0x002040C0u, 0x26E0C040u)]
    [InlineData(false, 8192, 0x002040C0u, 0x260000FFu)]
    public void Colored_depth_cue_uses_current_context_far_color_signed_IR0_and_original_rounding_without_changing_opcode_or_geometry(bool quad, short factor, uint farColor, uint expected)
    {
        byte[] original = Polygon(quad);
        byte[] polygon = new byte[original.Length + 4];
        original.CopyTo(polygon, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(original.Length), 0x26808080u | (quad ? 0x08000000u : 0));
        expected |= quad ? 0x08000000u : 0;
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleGeometryPacket unchanged = NativeBattleGeometryPackets.Emit50FDF0(polygon, Vertices, quad, farColor, 17, 4, 0x2000000, 0)!;
        NativeBattleGeometryPacket actual = NativeBattleGeometryPackets.Emit50FDF0(polygon, Vertices, quad, farColor, 17, 4, 0x2000000, factor, constants)!;
        Assert.Equal(expected, actual.Packet.Words[1]);
        Assert.Equal(unchanged.Packet.Words.SetItem(1, expected), actual.Packet.Words);
        Assert.Equal(unchanged.Bucket, actual.Bucket);
        Assert.Equal(unchanged.VertexPayload4, actual.VertexPayload4);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Original_body_packet_stores_preserve_UV_padding_and_unknown_heap_bytes_for_all_four_source_groups(bool quad, bool colored)
    {
        byte[] original = Polygon(quad);
        byte[] polygon = new byte[original.Length + (colored ? 4 : 0)];
        original.CopyTo(polygon, 0);
        if (colored)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(original.Length), quad ? 0x2E808080u : 0x26808080u);
        }
        NativeBattleGeometryPrimitive primitive = NativeBattleGeometryPackets.EmitRaw50FDF0(polygon, Vertices, quad, 0x808080, 17, 4, 0)!;
        int length = primitive.Words.Length * 4;
        ImmutableArray<byte> prior = Enumerable.Repeat((byte)0xA5, length).ToImmutableArray();
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattlePrimitiveProcess seeded = process.ApplyCommonWrite(0, prior, Enumerable.Repeat(NativeBattleGfPacketByteState.Known, length).ToImmutableArray());
        NativeBattlePrimitiveProcess written = NativeBattleCommonPacketArena.StorePrimitive(seeded, 0, primitive, false);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.ReadCommonPacket(30, 2));
        Assert.Equal((ushort)0x0605, BinaryPrimitives.ReadUInt16LittleEndian(written.ReadCommonPacket(28, 2)));
        Assert.Equal((uint)(quad ? 0x09000000 : 0x07000000), BinaryPrimitives.ReadUInt32LittleEndian(written.ReadCommonPacket(0, 4)));
        if (quad)
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.ReadCommonPacket(38, 2));
            Assert.Equal((ushort)0x0807, BinaryPrimitives.ReadUInt16LittleEndian(written.ReadCommonPacket(36, 2)));
        }
        NativeBattlePrimitiveProcess linked = NativeBattleCommonPacketArena.StorePrimitive(process, 0, primitive, true);
        Assert.Throws<NotSupportedException>(() => linked.ReadCommonPacket(0, 3));
        Assert.Throws<NotSupportedException>(() => linked.ReadCommonPacket(30, 2));
        Assert.Equal(primitive.Words[1], BinaryPrimitives.ReadUInt32LittleEndian(linked.ReadCommonPacket(4, 4)));
        NativeBattlePrimitiveProcess clone = JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(JsonSerializer.Serialize(written))!;
        Assert.Equal(written.ReadCommonPacket(0, length), clone.ReadCommonPacket(0, length));
    }

    [Fact]
    public void Mesh_submission_projects_original_WORDs_and_inserts_each_packet_in_physical_same_bucket_LIFO_order()
    {
        NativeBattleActionMatrix identity = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 0, 0, 0, 4096);
        BattleVertex[] vertices = [new(Vector3.Zero, 0) { NativePosition = new(0, 0, 1024) }, new(Vector3.Zero, 0) { NativePosition = new(10, 0, 1024) }, new(Vector3.Zero, 0) { NativePosition = new(0, 10, 1024) }];
        BattleFace first = new([2, 0, 1], [], 0, new(128, 128, 128)) { NativePolygon = Polygon(false).ToImmutableArray() };
        BattleFace second = first with { NativePolygon = Polygon(false).ToImmutableArray() };
        NativeBattleGeometrySubmission actual = NativeBattleActorPackets.SubmitMesh5099D0(NativeBattlePrimitiveOrdering.InitializeBattle(0), 0x2000000, new(vertices, [first, second]), [identity], new(identity, 512, 160, 108), 0x808080, null, 17, 4, face => face.NativePolygon.ToArray());
        Assert.Equal(2, actual.EmittedPackets);
        Assert.Equal(0x2000040u, actual.NextPacketAddress);
        Assert.Equal(NativeBattlePrimitiveOrdering.ProxyBankAddress + 24, actual.Ordering.Table[81]);
        byte[] lengths = new byte[256];
        lengths[0x24] = 7;
        ImmutableArray<NativeBattlePrimitiveCommand> ordered = actual.Ordering.Traverse(lengths.ToImmutableArray());
        Assert.Equal(new uint[] { 0x2000020, 0x2000000 }, ordered.Select(command => command.PacketAddress));
        Assert.All(actual.Ordering.Proxies, proxy => Assert.Equal(new int[] { 1026, 1026, 1026, 0 }, proxy.VertexPayload4));
    }

    [Fact]
    public void Relocated_triangle_retains_original_index_bias_and_rewrites_the_original_UV_word_positions()
    {
        BattleFace face = new([2, 0, 1], [], 0, new(128, 128, 128)) { NativePolygon = Polygon(false).ToImmutableArray(), NativeMaterial = new(0, 224 << 6, [new(1, 7), new(2, 8), new(3, 9)]) };
        NativeBattleModelTextureBinding binding = new(ImmutableArray.CreateRange(new byte?[16]).SetItem(0, 1));
        byte[] actual = NativeBattleActorPackets.RelocatePlayerPolygon507550(face, binding);
        Assert.Equal((ushort)0xA000, BinaryPrimitives.ReadUInt16LittleEndian(actual));
        Assert.Equal((ushort)(225 << 6), BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(10)));
        Assert.Equal((ushort)10, BinaryPrimitives.ReadUInt16LittleEndian(actual.AsSpan(14)));
        Assert.Equal(new byte[] { 1, 135 }, actual.AsSpan(6, 2).ToArray());
        Assert.Equal(new byte[] { 2, 136 }, actual.AsSpan(8, 2).ToArray());
        Assert.Equal(new byte[] { 3, 137 }, actual.AsSpan(12, 2).ToArray());
    }

    private static byte[] Polygon(bool quad)
    {
        byte[] bytes = new byte[quad ? 20 : 16];
        ushort[] words = quad ? [0xA000, 1, 2, 3, 0x0201, 0x1234, 0x0403, 0x40, 0x0605, 0x0807] : [0xA000, 1, 2, 0x0605, 0x0201, 0x1234, 0x0403, 0x40];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
        }
        return bytes;
    }
}
