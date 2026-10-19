using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleXatmDeath276PrimGeometryTests
{
    [Fact]
    public void Zero_effect_camera_is_consumed_without_substituting_the_current_view()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix zero = new(new short[9].ToImmutableArray(), 0, 0, 0, 4096);
        NativeBattleActionMatrix matrix = NativeBattleXatmDeath276PrimGeometry.Compose(Node(0x1000, new(40, 50, 60)), new(100, 200, 300), view, zero);
        Assert.All(matrix.Rotation, value => Assert.Equal((short)0, value));
        Assert.Equal((110, 220, 330), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void Direct_offset_stays_in_camera_axes_while_origin_uses_the_view()
    {
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix matrix = NativeBattleXatmDeath276PrimGeometry.Compose(Node(0x200, new(40, 50, 60)), new(100, 200, 300), view, view);
        Assert.Equal((-150, 170, 390), (matrix.X, matrix.Y, matrix.Z));
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, matrix.Rotation);
    }

    [Fact]
    public void Ordinary_origin_addition_wraps_WORDs_and_does_not_skip_matrix_composition_for_flag8000()
    {
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix matrix = NativeBattleXatmDeath276PrimGeometry.Compose(Node(0x8000, new(20, 30, 40)), new(32760, 0, 0), view, view);
        Assert.Equal((-20, -32736, 70), (matrix.X, matrix.Y, matrix.Z));
        Assert.Equal(view.Rotation, matrix.Rotation);
    }

    private static NativeBattleGfAnimationNode Node(uint flags, NativeBattleVector offset)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), offset.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), offset.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), offset.Z);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 4096);
        return new(0, 0, bytes.ToImmutableArray());
    }
}
