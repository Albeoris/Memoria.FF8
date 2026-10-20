using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlindTests
{
    [Theory]
    [InlineData(21, 0)]
    [InlineData(22, 0)]
    [InlineData(23, 625)]
    [InlineData(25, 1875)]
    [InlineData(26, 2496)]
    [InlineData(33, 312)]
    [InlineData(34, 0)]
    [InlineData(35, 0)]
    public void Flash_preserves_the_native_rising_and_falling_discontinuity(int age, int strength)
    {
        Assert.Equal(strength, NativeBattleBlindController.FlashAt(age));
    }

    [Theory]
    [InlineData(0u, 80, 218)]
    [InlineData(0x200u, 88, 210)]
    public void Screen_axis_offsets_remain_distinct_from_camera_rotated_world_offsets(uint flags, int x, int y)
    {
        NativeBattleActionMatrix camera = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 200, 300, 4096);
        byte[] record = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), flags);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(8), 8);
        for (int lane = 0; lane < 3; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(24 + lane * 2), 4096);
        }
        NativeBattleActionMatrix result = NativeBattleBlindDrawing.Matrix(new(0, 0, record.ToImmutableArray()), new(10, 20, 30), camera);
        Assert.Equal((x, y, 330), (result.X, result.Y, result.Z));
        Assert.Equal(flags == 0 ? camera.Rotation.ToArray() : new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, result.Rotation.ToArray());
    }

    [Fact]
    public void Additive_quad_reserves_its_separate_draw_mode_and_inserts_mode_before_quad_through_JSON()
    {
        byte[] source = new byte[68];
        BinaryPrimitives.WriteInt32LittleEndian(source, 12);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 4);
        int cursor = 12;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(cursor), group == 3 ? 1 : 0);
            cursor += 4;
            if (group != 3)
            {
                continue;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(cursor), 0x2E808080);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(cursor + 4 + corner * 2), (ushort)(corner * 2));
            }
            cursor += 24;
        }
        byte[] vertices = new byte[32];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8), (short)((corner & 1) == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(corner * 8 + 2), (short)((corner & 2) == 0 ? -100 : 100));
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleBlindDrawing.Additive(source.ToImmutableArray(), 0, vertices.ToImmutableArray(), matrix, projection, 0, 0, new(0, 0, 1f / 4096, 4503601774854144.0));
        Assert.Equal(48, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.Equal(40, batch.Attempts[0].ByteOffsetRelative);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000220 }, batch.Attempts[0].Primitive!.Words.ToArray());
        Assert.Equal(0, batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(0x09000000u, batch.Attempts[1].Primitive!.Words[0]);
        Assert.Equal(batch.Attempts[0].Primitive!.Bucket, batch.Attempts[1].Primitive!.Bucket);
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(batch))));
    }
}
