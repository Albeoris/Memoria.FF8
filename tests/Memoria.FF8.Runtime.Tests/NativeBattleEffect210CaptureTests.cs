using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210CaptureTests
{
    [Fact]
    public void Plan_preserves_native_quotient_remainder_divisors_and_signed_translation()
    {
        NativeBattleEffect210CapturePlan plan = NativeBattleEffect210CaptureMath.Prepare(Point(100, 300), Bounds(-96, -128, 96, 128), 288);
        Assert.Equal(2, plan.DivX);
        Assert.Equal(2, plan.DivZ);
        Assert.Equal(4096, plan.ScaleX);
        Assert.Equal(4096, plan.ScaleZ);
        Assert.Equal(96, plan.OffsetX);
        Assert.Equal(128, plan.OffsetY);
        NativeBattleActionMatrix matrix = plan.Matrix.ReadMatrix();
        Assert.Equal(new short[] { 4096, 0, 0, 0, 0, -4096, 0, 0, 0 }, matrix.Rotation.ToArray());
        Assert.Equal(-50, matrix.X);
        Assert.Equal(150, matrix.Y);
        Assert.Equal(288, matrix.Z);
        Assert.False(plan.Matrix.Known[18]);
        Assert.False(plan.Matrix.Known[19]);
        Assert.All(plan.Matrix.Known.Where((_, index) => index is not (18 or 19)), value => Assert.True(value));
        NativeBattleEffect210CapturePlan doubleWidth = NativeBattleEffect210CaptureMath.Prepare(Point(0, 0), Bounds(-192, -256, 192, 256), 288);
        Assert.Equal(3, doubleWidth.DivX);
        Assert.Equal(3, doubleWidth.DivZ);
        NativeBattleEffect210CapturePlan exactQuotient = NativeBattleEffect210CaptureMath.Prepare(Point(0, 0), Bounds(-18000, -128, 18864, 128), 288);
        Assert.Equal(192, exactQuotient.DivX);
    }

    [Fact]
    public void Odd_negative_centers_and_inverse_scales_use_separate_truncating_divisions()
    {
        NativeBattleEffect210CapturePlan plan = NativeBattleEffect210CaptureMath.Prepare(Point(-11, -11), Bounds(-101, -257, 284, 258), -300);
        NativeBattleActionMatrix matrix = plan.Matrix.ReadMatrix();
        Assert.Equal(3, plan.DivX);
        Assert.Equal(3, plan.DivZ);
        Assert.Equal(-27, matrix.X);
        Assert.Equal(-3, matrix.Y);
        Assert.Equal(-300, matrix.Z);
        Assert.Equal(2042, plan.ScaleX);
        Assert.Equal(2036, plan.ScaleZ);
        Assert.Equal(new short[] { 2042, 0, 0, 0, 0, -2036, 0, 0, 0 }, matrix.Rotation.ToArray());
        NativeBattleEffect210CapturePlan restored = JsonSerializer.Deserialize<NativeBattleEffect210CapturePlan>(JsonSerializer.Serialize(plan))!;
        Assert.Equal(plan.Matrix.Bytes.ToArray(), restored.Matrix.Bytes.ToArray());
        Assert.Equal(plan.Matrix.Known.ToArray(), restored.Matrix.Known.ToArray());
    }

    [Fact]
    public void Reversed_bounds_retain_negative_divisors_and_scales()
    {
        NativeBattleEffect210CapturePlan plan = NativeBattleEffect210CaptureMath.Prepare(Point(127, -33), Bounds(192, 256, -192, -256), 32767);
        Assert.Equal(-1, plan.DivX);
        Assert.Equal(-1, plan.DivZ);
        Assert.Equal(-2048, plan.ScaleX);
        Assert.Equal(-2048, plan.ScaleZ);
        Assert.Equal(127, plan.Matrix.ReadMatrix().X);
        Assert.Equal(33, plan.Matrix.ReadMatrix().Y);
        Assert.Equal(new short[] { -2048, 0, 0, 0, 0, 2048, 0, 0, 0 }, plan.Matrix.ReadMatrix().Rotation.ToArray());
    }

    [Theory]
    [InlineData(191, 256)]
    [InlineData(192, 255)]
    [InlineData(-192, 256)]
    public void Zero_native_divisors_fail_instead_of_clamping_or_substituting_geometry(short width, short height)
    {
        Assert.Throws<DivideByZeroException>(() => NativeBattleEffect210CaptureMath.Prepare(Point(0, 0), Bounds(0, 0, width, height), 288));
    }

    [Theory]
    [InlineData(false, 6, 24)]
    [InlineData(true, 9, 36)]
    public void Environment_uses_actual_opposite_state_and_only_native_written_bytes(bool clear, int wordCount, int lastWritten)
    {
        NativeBattleEffectArena retained = Retained(64);
        byte[] environment = new byte[92];
        Word(environment, 0, -9);
        Word(environment, 2, 511);
        Word(environment, 4, 17);
        Word(environment, 6, -2);
        Word(environment, 8, 704);
        Word(environment, 10, 256);
        Word(environment, 0x14, 0x123);
        environment[0x18] = clear ? (byte)1 : (byte)0;
        environment[0x19] = 0x12;
        environment[0x1A] = 0x34;
        environment[0x1B] = 0x56;
        NativeBattleGfGeometryBatch batch = NativeBattleEffect210CapturePackets.Environment(retained, environment.ToImmutableArray());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(64, batch.AllocationBytes);
        Assert.Equal(wordCount, attempt.Primitive!.Words.Length);
        Assert.Equal(17, attempt.Primitive.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal(clear ? 0x08A5A5A5u : 0x05A5A5A5u, attempt.Primitive.Words[0]);
        AssertMask(attempt, 3, lastWritten);
        Assert.All(attempt.Data.AsSpan().Slice(lastWritten).ToArray(), value => Assert.Equal((byte)0xA5, value));
        if (clear)
        {
            Assert.Equal(0x02563412u, attempt.Primitive.Words[6]);
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect210CapturePackets.Environment(retained, new byte[91].ToImmutableArray()));
    }

    [Fact]
    public void Black_quad_sets_full_tag_and_has_no_depth_sidecars()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleEffect210CapturePackets.Black(Retained(24));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(24, batch.AllocationBytes);
        Assert.Equal(17, attempt.Primitive!.Bucket);
        Assert.Equal(new uint[] { 0x05000000, 0x28000000, 0, 192, 0x01000000, 0x010000C0 }, attempt.Primitive.Words.ToArray());
        Assert.Empty(attempt.Primitive.VertexPayload4);
        AssertMask(attempt, 0, 24);
    }

    [Fact]
    public void Area_and_offset_keep_low_TAG_and_offset_allocation_tail_unwritten()
    {
        NativeBattleGfGeometryBatch area = NativeBattleEffect210CapturePackets.Area(Retained(12));
        NativeBattleGfGeometryAttempt areaAttempt = Assert.Single(area.Attempts);
        Assert.Equal(new uint[] { 0x02A5A5A5, 0xE30402C0, 0xE407FF7F }, areaAttempt.Primitive!.Words.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0 }, areaAttempt.Primitive.VertexPayload4);
        AssertMask(areaAttempt, 3, 12);
        NativeBattleGfGeometryBatch offset = NativeBattleEffect210CapturePackets.Offset(Retained(12));
        NativeBattleGfGeometryAttempt offsetAttempt = Assert.Single(offset.Attempts);
        Assert.Equal(12, offset.AllocationBytes);
        Assert.Equal(12, offsetAttempt.Data.Length);
        Assert.Equal(new uint[] { 0x01A5A5A5, 0xE50802C0 }, offsetAttempt.Primitive!.Words.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0 }, offsetAttempt.Primitive.VertexPayload4);
        AssertMask(offsetAttempt, 3, 8);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, offsetAttempt.Data.AsSpan().Slice(8, 4).ToArray());
    }

    private static void AssertMask(NativeBattleGfGeometryAttempt attempt, int first, int last)
    {
        for (int index = 0; index < attempt.Data.Length; index++)
        {
            Assert.Equal(index >= first && index < last ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[index]);
        }
    }

    private static NativeBattleEffectArena Retained(int length) => NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, length).ToArray()).MarkUnknown(0, length);
    private static NativeBattleEffectArena Point(short x, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(z));
    private static NativeBattleEffectArena Bounds(short left, short top, short right, short bottom) => NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, BitConverter.GetBytes(left)).Write(4, BitConverter.GetBytes(top)).Write(6, BitConverter.GetBytes(right)).Write(10, BitConverter.GetBytes(bottom));
    private static void Word(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
}
