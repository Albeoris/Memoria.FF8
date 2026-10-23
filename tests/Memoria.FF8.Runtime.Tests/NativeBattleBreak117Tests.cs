using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreak117Tests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3640)]
    [InlineData(8, 29127)]
    [InlineData(9, 32768)]
    [InlineData(47, 32768)]
    [InlineData(48, 32768)]
    [InlineData(59, 2730)]
    public void Stone_level_keeps_original_nine_frame_rise_and_twelve_frame_fall(int age, int expected)
    {
        Assert.Equal(expected, NativeBattleBreak117Body.StoneLevel(age));
    }

    [Fact]
    public void Stone_material_replaces_exact_clut_page_and_preserves_uv_color_depth_and_padding()
    {
        byte[] polygon = new byte[16];
        Put(2, 1);
        Put(4, 2);
        Put(6, 0x3123);
        Put(8, 0x1022);
        Put(10, 0x1234);
        Put(12, 0x2044);
        Put(14, 0x3AA);
        NativeBattleProjectedVertex[] projected = [new(0, 100), new(128, 200), new(128u << 16, 300)];
        NativeBattleGeometryPrimitive stone = NativeBattleBreak117Body.Polygon(polygon.ToImmutableArray(), false, projected, 0x123456, true)!;
        Assert.Equal(0x24123456u, stone.Words[1]);
        Assert.Equal(0x3D541022u, stone.Words[3]);
        Assert.Equal(0x00B82044u, stone.Words[5]);
        Assert.Equal(0x3123u, stone.Words[7]);
        Assert.Equal(new[] { 400, 800, 1200, 0 }, stone.VertexPayload4);
        Assert.Equal(17 + (600 * 341 >> 12), stone.Bucket);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, stone.PhysicalWriteMask!.Value[30]);
        NativeBattleGeometryPrimitive original = NativeBattleBreak117Body.Polygon(polygon.ToImmutableArray(), false, projected, 0x123456, false)!;
        Assert.Equal(0x12341022u, original.Words[3]);
        Assert.Equal(0x01AA2044u, original.Words[5]);

        void Put(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(offset), value);
    }

    [Theory]
    [InlineData(4096, 1024)]
    [InlineData(65535, 16383)]
    public void Stone_body_projects_quarter_depth_before_clipping_ordering_and_proxy_expansion(int cameraDepth, ushort scratchDepth)
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-1000, -1000, 0), 0), new(new(1000, -1000, 0), 0), new(new(-1000, 1000, 0), 0)], [new(false, polygon.ToImmutableArray())]);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = cameraDepth };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);

        NativeBattleBreak117BodyResult result = NativeBattleBreak117Body.Draw([mesh], [matrix], projection, 0x123456, 1, new byte[512].ToImmutableArray(), 32768, null, 0);

        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(scratchDepth, BinaryPrimitives.ReadUInt16LittleEndian(result.Projected.AsSpan().Slice(index * 8 + 4)));
            Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Projected.AsSpan().Slice(index * 8 + 6)));
        }
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Primitives);
        Assert.Equal(17 + (scratchDepth * 3 * 341 >> 12), primitive.Bucket);
        Assert.InRange(primitive.Bucket, 0, NativeBattlePrimitiveOrdering.BattleTableLength - 1);
        Assert.Equal(new[] { scratchDepth * 4, scratchDepth * 4, scratchDepth * 4, 0 }, primitive.VertexPayload4);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(primitive.Bucket, new("test", 0, 0), primitive.Words, 0, primitive.VertexPayload4);
        Assert.Single(ordering.Packets);
    }

    [Theory]
    [InlineData(4096, 1024)]
    [InlineData(65535, 16383)]
    public void Crossing_flat_triangle_keeps_native_seven_word_tag_and_forty_byte_allocation(int cameraDepth, ushort scratchDepth)
    {
        byte[] source = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(source, 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(20), 0x24123456);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(26), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(28), 4);
        byte[] vertices = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(vertices, -100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(2), -100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(10), -100);
        BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(18), 100);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = cameraDepth };
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleBreak117CutResult result = NativeBattleBreak117Cut.Draw(source.ToImmutableArray(), 0, vertices.ToImmutableArray(), matrix, projection, 0, new byte[24].ToImmutableArray());
        Assert.Equal(40, result.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(result.Packets.Attempts);
        Assert.Equal(0x7000000u, packet.Primitive!.Words[0]);
        Assert.Equal(0x24123456u, packet.Primitive.Words[1]);
        Assert.Equal(10, packet.Primitive.Words.Length);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[38]);
        for (int index = 0; index < 3; index++)
        {
            Assert.Equal(scratchDepth, BinaryPrimitives.ReadUInt16LittleEndian(result.Projected.AsSpan().Slice(index * 8 + 4)));
            Assert.NotEqual((byte)16, result.Projected[index * 8 + 6]);
        }
        Assert.Equal(17 + (scratchDepth * 3 * 341 >> 12), packet.Primitive.Bucket);
        Assert.InRange(packet.Primitive.Bucket, 0, NativeBattlePrimitiveOrdering.BattleTableLength - 1);
        Assert.Equal(new[] { scratchDepth * 4, scratchDepth * 4, scratchDepth * 4, scratchDepth * 4 }, packet.Primitive.VertexPayload4);
    }

    [Fact]
    public void Motion_uses_signed_shift_bounce_and_independent_JSON_continuation()
    {
        NativeBattleModulePoolTask debris = Task(0x30).WriteWord(18, unchecked((ushort)-1)).WriteWord(26, 10).WriteWord(40, 100);
        debris = NativeBattleBreak117Geometry.DebrisMove(debris);
        Assert.Equal((ushort)0, debris.ReadWord(18));
        Assert.Equal(unchecked((ushort)-15), debris.ReadWord(26));
        Assert.Equal((ushort)100, debris.ReadWord(32));
        NativeBattleModulePoolTask cold = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(debris))!;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleBreak117Geometry.DebrisMove(debris)), JsonSerializer.Serialize(NativeBattleBreak117Geometry.DebrisMove(cold)));
        NativeBattleModulePoolTask spark = Task(0x28).WriteWord(24, 5).WriteWord(32, unchecked((ushort)-1));
        spark = NativeBattleBreak117Geometry.SparkMove(spark);
        Assert.Equal((ushort)4, spark.ReadWord(24));
        Assert.Equal((ushort)4, spark.ReadWord(16));
    }

    private static NativeBattleModulePoolTask Task(int size)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, checked((ushort)size), 1);
        (pool, int slot) = pool.Append(0x3000);
        return pool.Slots[slot] with { Payload = new byte[size - 12].ToImmutableArray() };
    }
}
