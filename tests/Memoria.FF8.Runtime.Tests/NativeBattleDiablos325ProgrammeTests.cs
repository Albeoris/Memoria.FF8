using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325ProgrammeTests
{
    [Fact]
    public void Ordinary_position_addition_wraps_WORD_before_the_single_view_rotation()
    {
        NativeBattleActionMatrix view = new([2048, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix wrapped = NativeBattleDiablos325Programme.Compose656B70(Node(position: new(10000, 2, 3)), new(30000, 4, 5), view);
        Assert.Equal((-12758, 26, 38), (wrapped.X, wrapped.Y, wrapped.Z));
        NativeBattleActionMatrix direct = NativeBattleDiablos325Programme.Compose656B70(Node(0x200, position: new(10000, 2, 3)), new(30000, 4, 5), view);
        Assert.Equal((25010, 26, 38), (direct.X, direct.Y, direct.Z));
    }

    [Fact]
    public void Local_rotation_precedes_view_and_missing_billboard_matrix_remains_unknown()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix rotated = NativeBattleDiablos325Programme.Compose656B70(Node(0x800, new(10, 0, 0), new(0, 0, 1024)), new(0, 0, 0), view);
        Assert.Equal((0, -10, 0), (rotated.X, rotated.Y, rotated.Z));
        NativeBattleGfAnimationNode billboard = Node(0x1000, new(10, 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Programme.Compose656B70(billboard, new(1, 2, 3), view));
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Euler701310(new(0, 0, 1024));
        NativeBattleActionMatrix known = NativeBattleDiablos325Programme.Compose656B70(billboard, new(1, 2, 3), view, effectCamera: basis);
        Assert.Equal((1, -8, 3), (known.X, known.Y, known.Z));
    }

    [Fact]
    public void Node_scale_preserves_integer_SAR_vs_rounded_matrix_product_and_context_scale_affects_position_first()
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix rows = NativeBattleDiablos325Programme.Compose656B70(Node(rotation: new(0, 0, 1024), scale: new(1024, 2048, 4096)), new(0, 0, 0), view);
        NativeBattleActionMatrix columns = NativeBattleDiablos325Programme.Compose656B70(Node(0x100, rotation: new(0, 0, 1024), scale: new(1024, 2048, 4096)), new(0, 0, 0), view);
        Assert.Equal(new short[] { 0, 2048, 0, -1024, 0, 0, 0, 0, 4096 }, rows.Rotation);
        Assert.Equal(new short[] { 0, 2048, 0, -1024, 0, 0, 0, 0, 4096 }, columns.Rotation);
        NativeBattleActionMatrix fractional = view with { Rotation = [3, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        Assert.Equal((short)1, NativeBattleDiablos325Programme.Compose656B70(Node(scale: new(2048, 4096, 4096)), new(0, 0, 0), fractional).Rotation[0]);
        Assert.Equal((short)2, NativeBattleDiablos325Programme.Compose656B70(Node(0x100, scale: new(2048, 4096, 4096)), new(0, 0, 0), fractional).Rotation[0]);
        NativeBattleActionMatrix position = NativeBattleDiablos325Programme.Compose656B70(Node(position: new(8, 16, 32)), new(1, 2, 3), view, new(2048, 1024, 4096));
        Assert.Equal((5, 6, 35), (position.X, position.Y, position.Z));
    }

    private static NativeBattleGfAnimationNode Node(uint header = 0, NativeBattleVector? position = null, NativeBattleVector? rotation = null, NativeBattleVector? scale = null)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Put(8, position ?? new(0, 0, 0));
        Put(16, rotation ?? new(0, 0, 0));
        Put(24, scale ?? new(4096, 4096, 4096));
        return new(0, 0, bytes.ToImmutableArray());

        void Put(int offset, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), value.Z);
        }
    }
}
