using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSlow123Tests
{
    [Theory]
    [InlineData(19, 9)]
    [InlineData(20, 1)]
    [InlineData(21, 1)]
    [InlineData(40, 9)]
    [InlineData(59, 9)]
    [InlineData(60, 9)]
    public void Blink_keeps_native_unsigned_window_and_modulo_boundary(int counter, ushort expected)
    {
        Assert.Equal(expected, NativeBattleSlow123Geometry.BlinkFlags(9, counter, false));
        Assert.Equal((ushort)1, NativeBattleSlow123Geometry.BlinkFlags(9, counter, true));
    }

    [Fact]
    public void Borrowed_scale_flag_is_normalized_effect_camera_row_one_column_zero()
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix context = identity with { X = 100 };
        NativeBattleActionMatrix ordinary = NativeBattleSlow123Geometry.Compose(Node(), context, identity, 2048);
        Assert.Equal(300, ordinary.X);
        Assert.Equal((short)4096, ordinary.Rotation[0]);
        NativeBattleActionMatrix roll = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix scaled = NativeBattleSlow123Geometry.Compose(Node(), context, roll, 2048);
        Assert.Equal(200, scaled.X);
        Assert.Equal((short)2048, scaled.Rotation[0]);
    }

    private static NativeBattleGfAnimationNode Node()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 200);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
