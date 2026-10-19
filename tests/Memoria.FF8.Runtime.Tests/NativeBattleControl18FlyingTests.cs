using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl18FlyingTests
{
    [Fact]
    public void Expired_active_records_clear_the_active_byte_without_consuming_a_packet()
    {
        byte[] records = new byte[56];
        records[24] = 1;
        records[25] = 0;
        records[28 + 25] = 12;
        NativeBattleControl18FlyingStep result = Advance(records, 2);
        Assert.Equal((byte)0, result.Records[24]);
        Assert.Equal((byte)255, result.Records[25]);
        Assert.Equal((byte)12, result.Records[28 + 25]);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
    }

    [Fact]
    public void Acceleration_wraps_as_signed_WORD_before_its_shifted_DWORD_position_addition()
    {
        byte[] record = Record();
        Word(record, 12, short.MaxValue);
        Word(record, 14, 1);
        Word(record, 16, -2);
        Word(record, 18, 1);
        Word(record, 20, 3);
        Word(record, 22, -1);
        NativeBattleControl18FlyingStep result = Advance(record, 2);
        Assert.Equal(-524288, Dword(result, 0));
        Assert.Equal(-16, Dword(result, 4));
        Assert.Equal(32, Dword(result, 8));
        Assert.Equal((short)-32768, BinaryPrimitives.ReadInt16LittleEndian(result.Records.AsSpan(12, 2)));
        Assert.Equal((byte)0, result.Records[25]);
        Assert.Equal(12, result.Batch.AllocationBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Positive_Y_resets_vertical_velocity_and_acceleration_after_movement_without_clamping_position(int mode)
    {
        byte[] record = Record();
        Word(record, 16, 1);
        Word(record, 18, 2);
        NativeBattleControl18FlyingStep result = Advance(record, (ushort)mode);
        Assert.Equal(48, Dword(result, 4));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(result.Records.AsSpan(16, 2)));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(result.Records.AsSpan(18, 2)));
    }

    [Fact]
    public void Mode4_draws_without_decrement_and_keeps_DMA_link_bytes_untouched()
    {
        byte[] record = Record();
        NativeBattleControl18FlyingStep result = Advance(record, 4, 4099);
        Assert.Equal(record, result.Records.ToArray());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.All(attempt.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.All(attempt.WriteMask.Skip(3), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(0x6A654321u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(4, 4)));
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(17 + 256, primitive.Bucket);
        Assert.Equal(new[] { 4096, 4096, 4096, 4096 }, primitive.VertexPayload4);
    }

    [Theory]
    [InlineData(65519, true)]
    [InlineData(65520, false)]
    public void Depth_insertion_guard_is4095_and_rejection_still_consumes_twelve_bytes(int depth, bool accepted)
    {
        NativeBattleControl18FlyingStep result = Advance(Record(), 4, depth);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(12, result.Batch.AllocationBytes);
        Assert.Equal(accepted, attempt.Primitive is not null);
        Assert.Equal((byte)2, attempt.Data[3]);
    }

    [Fact]
    public void Cylindrical_mode_uses_angle_SAR4_and_radius_SAR8_before_the_trigonometric_products()
    {
        byte[] record = Record();
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(), 2560);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(8), 1024 << 4);
        NativeBattleControl18FlyingStep result = Advance(record, 6);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        uint screen = BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(8, 4));
        NativeBattleCameraProjection projection = Projection(4096);
        uint expected = NativeBattleModuleProjection.Project45FE10(projection.View!, new(10, 0, 0), projection).Screen;
        Assert.Equal(expected, screen);
    }

    private static NativeBattleControl18FlyingStep Advance(byte[] records, ushort mode, int depth = 4096)
    {
        NativeBattleCameraProjection projection = Projection(depth);
        return NativeBattleControl18Flying.Advance(records, mode, new uint[] { 0x123456, 0x654321 }, point => NativeBattleModuleProjection.Project45FE10(projection.View!, point, projection), 17);
    }

    private static NativeBattleCameraProjection Projection(int depth) => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, depth, 4096), 216, 160, 108);
    private static byte[] Record()
    {
        byte[] record = new byte[28];
        record[24] = 1;
        record[25] = 1;
        return record;
    }
    private static void Word(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static int Dword(NativeBattleControl18FlyingStep result, int offset) => BinaryPrimitives.ReadInt32LittleEndian(result.Records.AsSpan(offset, 4));
}
