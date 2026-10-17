using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlGeometryPacketsTests
{
    private static NativeBattleQuezacotlGeometryContext Context => new(0, 0, 0, new(0, 0, 256, 256), new(256, 128, 256, 128), 17, 2);

    [Theory]
    [InlineData(false, 40, 64, 272)]
    [InlineData(true, 52, 76, 273)]
    public void Native_GT_packet_and_texture_windows_keep_store_order_link_order_and_exact_allocation(bool quad, int polygonLength, int allocation, int bucket)
    {
        NativeBattleGfGeometryBatch batch = Emit(quad, Face(quad), Corners(quad), Context);
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(4, batch.Attempts.Length);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.All(batch.Attempts[0].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Equal(new[] { 0, polygonLength, 0, polygonLength + 12 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.Attempts[2].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        NativeBattleGeometryPrimitive polygon = Assert.IsType<NativeBattleGeometryPrimitive>(batch.Attempts[2].Primitive);
        Assert.Equal(bucket, polygon.Bucket);
        Assert.Equal((uint)(polygonLength / 4 - 1) << 24, polygon.Words[0]);
        Assert.Equal(quad ? 0x3E404040u : 0x36404040u, polygon.Words[1]);
        Assert.Equal(XY(100, 100), polygon.Words[2]);
        Assert.Equal(0x12340201u, polygon.Words[3]);
        Assert.Equal(0xAA112233u, polygon.Words[4]);
        Assert.Equal(0xABCD0403u, polygon.Words[6]);
        Assert.Equal(quad ? 0x08070605u : 0x0605u, polygon.Words[9]);
        if (quad)
        {
            Assert.Equal(0xCC778899u, polygon.Words[10]);
            Assert.Equal(0x0807u, polygon.Words[12]);
        }
        Assert.Equal(new uint[] { 0x02000000, 0xE2000000, 0 }, batch.Attempts[1].Primitive!.Words);
        Assert.Equal(new uint[] { 0x02000000, 0xE2080200, 0 }, batch.Attempts[3].Primitive!.Words);
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts.Where(attempt => attempt.Primitive is not null))
        {
            int[] expectedPayload = [0, 0, 0, 0];
            if (attempt.PacketLength == polygonLength)
            {
                expectedPayload = quad ? [4096, 4096, 4096, 4096] : [4096, 4096, 4096, 0];
            }
            Assert.Equal(expectedPayload, attempt.Primitive!.VertexPayload4);
            Assert.Equal(bucket, attempt.Primitive.Bucket);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void First_three_near_flags_leave_only_native_prefix_writes_and_never_read_the_fourth_vertex(bool quad)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners(quad);
        corners[1] = corners[1] with { Flags = 0x80020000 };
        NativeBattleGfGeometryBatch batch = Emit(quad, Face(quad), new FourthMustNotBeRead(corners), Context);
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Screen_reject_preserves_projected_coordinates_but_not_late_colors_and_does_not_allocate(bool quad)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners(quad).Select(vertex => vertex with { Screen = XY(-1, 100) }).ToArray();
        NativeBattleGfGeometryBatch batch = Emit(quad, Face(quad), corners, Context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
        Assert.Equal(XY(-1, 100), BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(8)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Native_custom_leaves_admit_backfaces_and_ignore_fourth_RTPS_flags(bool quad)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners(quad);
        (corners[1], corners[2]) = (corners[2], corners[1]);
        if (quad)
        {
            corners[3] = corners[3] with { Flags = 0x80060000 };
        }
        Assert.Equal(4, Emit(quad, Face(quad), corners, Context).Attempts.Length);
    }

    [Theory]
    [InlineData(false, 16, 18, 20, 22)]
    [InlineData(true, 16, 18, 20, 22)]
    [InlineData(false, 250, 124, 126, 128)]
    [InlineData(true, 250, 124, 126, 128)]
    [InlineData(false, 256, 130, 132, 134)]
    [InlineData(true, 256, 130, 132, 134)]
    public void One_overflowing_V_coordinate_selects_byte_subtraction_for_every_corner(bool quad, ushort offset, byte first, byte second, byte third)
    {
        NativeBattleGfGeometryBatch batch = Emit(quad, Face(quad), Corners(quad), Context with { VerticalOffset = offset });
        NativeBattleGeometryPrimitive polygon = batch.Attempts[2].Primitive!;
        Assert.Equal(first, (byte)(polygon.Words[3] >> 8));
        Assert.Equal(second, (byte)(polygon.Words[6] >> 8));
        Assert.Equal(third, (byte)(polygon.Words[9] >> 8));
        Assert.Equal(quad ? 273 : 272, polygon.Bucket);
    }

    [Theory]
    [InlineData(false, 0x36000000u)]
    [InlineData(true, 0xCC000000u)]
    public void Depth_cue_reuses_PC_color_interpolation_with_native_FIFO_command_byte(bool quad, uint fifoCommand)
    {
        NativeBattleSummonColors colors = new(0, 0, 1f / 4096, 4503601774854144.0);
        NativeBattleQuezacotlGeometryContext context = Context with { DepthCue = 2048, FarColor = 0x804020 };
        NativeBattleGfGeometryBatch batch = Emit(quad, Face(quad), Corners(quad), context, colors);
        NativeBattleGeometryPrimitive polygon = batch.Attempts[2].Primitive!;
        Assert.Equal(colors.Interpolate(quad ? 0x3E404040u : 0x36404040u, 2048, context.FarColor), polygon.Words[1]);
        Assert.Equal((colors.Interpolate(0xAA112233, 2048, context.FarColor) & 0xFFFFFF) | fifoCommand, polygon.Words[4]);
        Assert.Throws<NotSupportedException>(() => Emit(quad, Face(quad), Corners(quad), context));
    }

    [Fact]
    public void Native45C7A0_selects_payload_from_opcode_and_actual_current_depth_FIFO()
    {
        byte[] face = Face(true);
        NativeBattleModuleProjectedVertex[] corners = Corners(true);
        for (int index = 0; index < corners.Length; index++)
        {
            corners[index] = corners[index] with { Depth = (ushort)(100 + index) };
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x36404040);
        Assert.Equal(new int[] { 101, 102, 103, 0 }, Emit(true, face, corners, Context).Attempts[2].Primitive!.VertexPayload4);
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x62404040);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, Emit(true, face, corners, Context).Attempts[2].Primitive!.VertexPayload4);
        face = Face(false);
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x3E404040);
        Assert.Throws<NotSupportedException>(() => Emit(false, face, Corners(false), Context));
        Assert.Equal(new int[] { 77, 4096, 4096, 4096 }, Emit(false, face, Corners(false), Context with { PriorDepthFifo = 77 }).Attempts[2].Primitive!.VertexPayload4);
    }

    [Fact]
    public void Wrapper_starts_at_geometry_table_plus18_and_preserves_rejected_cursor_overlap_through_JSON()
    {
        byte[] source = new byte[128];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(56), 1);
        Face(false).CopyTo(source, 60);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(88), 1);
        Face(true).CopyTo(source, 92);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(96 + index * 2), (ushort)(((index + 1) & 3) * 2));
        }
        byte[] vertices = new byte[32];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 8), (short)(index * 16));
        }
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(4), -8192);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleQuezacotlGeometryPackets.EmitBatch6C5710(source.ToImmutableArray(), 16, vertices.ToImmutableArray(), matrix, projection, Context);
        Assert.Equal(76, batch.AllocationBytes);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(new[] { 0, 0, 52, 0, 64 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        string json = JsonSerializer.Serialize(batch);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(json)));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlGeometryPackets.EmitBatch6C5710(source.ToImmutableArray(), 16, [], matrix, projection, Context));
    }

    [Theory]
    [InlineData(0, 0, 256, 256, 0xE2000000u)]
    [InlineData(256, 128, 256, 128, 0xE2080200u)]
    [InlineData(24, 40, 32, 64, 0xE2028F1Cu)]
    public void Texture_window_packet_uses_native_BYTE_origins_and_WORD_size_complements(short x, short y, short width, short height, uint expected)
    {
        Assert.Equal(expected, NativeBattleQuezacotlGeometryPackets.TextureWindowCommand(new(x, y, width, height)));
    }

    private static NativeBattleGfGeometryBatch Emit(bool quad, byte[] face, IReadOnlyList<NativeBattleModuleProjectedVertex> vertices, NativeBattleQuezacotlGeometryContext context, NativeBattleSummonColors? colors = null)
    {
        return quad ? NativeBattleQuezacotlGeometryPackets.EmitQuad6C5B50(face, vertices, context, colors) : NativeBattleQuezacotlGeometryPackets.EmitTriangle6C5750(face, vertices, context, colors);
    }

    private static byte[] Face(bool quad)
    {
        byte[] result = new byte[quad ? 36 : 28];
        BinaryPrimitives.WriteUInt32LittleEndian(result, quad ? 0x3E404040u : 0x36404040u);
        for (int index = 0; index < (quad ? 4 : 3); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4 + index * 2), (ushort)(index * 2));
        }
        if (!quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 0x0605);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), 0xABCD0403);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(20), 0x08070605);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(quad ? 24 : 20), 0xAA112233);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(quad ? 28 : 24), 0xBB445566);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(32), 0xCC778899);
        }
        return result;
    }

    private static NativeBattleModuleProjectedVertex[] Corners(bool quad)
    {
        NativeBattleModuleProjectedVertex[] vertices = [new(XY(100, 100), 4096, 0), new(XY(200, 100), 4096, 0), new(XY(100, 200), 4096, 0), new(XY(200, 200), 4096, 0)];
        return quad ? vertices : vertices[..3];
    }

    private static uint XY(short x, short y) => unchecked((ushort)x | (uint)(ushort)y << 16);

    private sealed class FourthMustNotBeRead(NativeBattleModuleProjectedVertex[] vertices) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => vertices.Length;
        public NativeBattleModuleProjectedVertex this[int index] => index == 3 ? throw new InvalidOperationException("Native near rejection must not execute fourth RTPS.") : vertices[index];
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => ((IEnumerable<NativeBattleModuleProjectedVertex>)vertices).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
