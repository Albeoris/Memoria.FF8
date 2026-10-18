using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSearchSpritesTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 1000, 0, 0);

    [Theory]
    [InlineData(-64, 63)]
    [InlineData(-300, 17)]
    [InlineData(67, 96)]
    public void Signed_depth_bias_is_added_before_shift_and_clamped_before_bucket_selection(short bias, int bucket)
    {
        ImmutableArray<byte> resource = NativeBattleCuragaProjectedSpritesTests.Resource(false, NativeBattleCuragaProjectedSpritesTests.Quad());
        NativeBattleGeometryPrimitive unbiased = Assert.Single(NativeBattleCuragaProjectedSprites.Emit8DD260(resource, 0, View, Projection, 0, 17, 2).Primitives).Primitive;
        NativeBattleGeometryPrimitive biased = Assert.Single(NativeBattleCuragaProjectedSprites.Emit8DD260(resource, 0, View, Projection, 0, 17, 2, bias).Primitives).Primitive;
        Assert.Equal(bucket, biased.Bucket);
        Assert.Equal(unbiased.Words, biased.Words);
        Assert.Equal(unbiased.VertexPayload4, biased.VertexPayload4);
    }

    [Fact]
    public void First_accepted_bucket_remains_shared_after_initial_rejection_and_extended_mode_insertion()
    {
        byte[] second = NativeBattleCuragaProjectedSpritesTests.Quad();
        byte[] third = NativeBattleCuragaProjectedSpritesTests.Quad();
        BinaryPrimitives.WriteInt16LittleEndian(second.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(third.AsSpan(8), 200);
        ImmutableArray<byte> resource = NativeBattleCuragaProjectedSpritesTests.Resource(true, NativeBattleCuragaProjectedSpritesTests.Quad(), second, third);
        NativeBattleActionMatrix varying = View with { Rotation = [4096, 0, 0, 0, 4096, 0, 4096, 0, 4096], Z = 50 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, 0, varying, new(null, 216, 160, 108), 0, 17, 2, -64);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(92, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 0, 40, 0, 52 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(112, attempt.Primitive!.Bucket));
    }

    [Fact]
    public void Adapter_uses_rounded_view_times_transpose_and_keeps_full_camera_translation()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x1C, 4096);
        NativeBattleActionMatrix view = View with { Rotation = [4095, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 70000 };
        NativeBattleActionMatrix matrix = NativeBattleSearchSprites.Matrix(task, view);
        Assert.Equal((short)4094, matrix.Rotation[0]);
        Assert.Equal(74095, matrix.X);
        Assert.Equal(1000, matrix.Z);
        ImmutableArray<byte> resource = NativeBattleCuragaProjectedSpritesTests.Resource(false, NativeBattleCuragaProjectedSpritesTests.Quad());
        NativeBattleGfGeometryBatch drawn = NativeBattleSearchSprites.Draw7582A0(Task().WriteWord(0x56, unchecked((ushort)-64)), resource, View, Projection, 0);
        Assert.Equal(63, Assert.Single(drawn.Attempts).Primitive!.Bucket);
        Assert.Empty(NativeBattleSearchSprites.Draw7582A0(Task().WriteByte(0x26, 4), [], view, Projection, 0).Attempts);
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x7582A0, new byte[0x88 - 12].ToImmutableArray());
}
