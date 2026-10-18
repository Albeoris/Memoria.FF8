using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMoogle96ProgramDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(0x100u, 2)]
    public void Header100_uses_biased_matrix_multiply_instead_of_arithmetic_column_scaling(uint header, short expected)
    {
        NativeBattleActionMatrix context = View with { Rotation = [3, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        NativeBattleActionMatrix matrix = NativeBattleMoogle96ProgramDrawing.ComposeNodeMatrix(Node(header, scale: new(2048, 4096, 4096)), context);
        Assert.Equal(expected, matrix.Rotation[0]);
    }

    [Fact]
    public void Euler400_branch_and_full_MAC_position_always_include_context_basis_even_with_other_header_bits()
    {
        NativeBattleActionMatrix context = View with { Rotation = [8192, 4096, 0, 0, 4096, 0, 0, 0, 4096], X = 100, Y = 200 };
        NativeBattleVector angles = new(512, 768, 1024);
        NativeBattleGfAnimationNode node = Node(0x400, new(30000, 20000, 0), angles);
        NativeBattleActionMatrix actual = NativeBattleMoogle96ProgramDrawing.ComposeNodeMatrix(node, context);
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Multiply56C090(context, NativeBattleGfMatrices.Euler701430(angles));
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((80100, 20200, 4096), (actual.X, actual.Y, actual.Z));
        NativeBattleActionMatrix ordinary = NativeBattleMoogle96ProgramDrawing.ComposeNodeMatrix(Node(0, new(30000, 20000, 0), angles), context);
        Assert.NotEqual(ordinary.Rotation.ToArray(), actual.Rotation.ToArray());
        NativeBattleActionMatrix unused = NativeBattleMoogle96ProgramDrawing.ComposeNodeMatrix(Node(0x49200, new(30000, 20000, 0), angles), context);
        Assert.Equal(ordinary.Rotation.ToArray(), unused.Rotation.ToArray());
        Assert.Equal((ordinary.X, ordinary.Y, ordinary.Z), (unused.X, unused.Y, unused.Z));
    }

    [Fact]
    public void Node_skip_gates_need_no_geometry_but_nonzero_shape_and_partial_morph_are_unsupported()
    {
        NativeBattleGfAnimationSource absent = new(0, []);
        Assert.Empty(NativeBattleMoogle96ProgramDrawing.Node734580(absent, Node(scale: new(0, 0, 0)), View, Projection, Colors).Attempts);
        byte[] bytes = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 4096);
        Assert.Empty(NativeBattleMoogle96ProgramDrawing.Node734580(absent, Node() with { Data = bytes.ToImmutableArray() }, View, Projection, Colors).Attempts);
        bytes = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96ProgramDrawing.Node734580(Source(), Node() with { Data = bytes.ToImmutableArray() }, View, Projection, Colors));
        bytes = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleMoogle96ProgramDrawing.Node734580(Source(), Node() with { Data = bytes.ToImmutableArray() }, View, Projection, Colors));
    }

    [Fact]
    public void UV_update_subtracts128_from_every_corner_after_any_overflow_and_preserves_every_other_byte()
    {
        NativeBattleGfAnimationSource source = Source();
        byte[] expected = source.Data.ToArray();
        foreach (int index in new[] { 13, 14, 15, 19, 20 })
        {
            int geometry = checked((int)U32(source.Data, 8 + index * 4));
            int cursor = geometry + checked((int)U32(source.Data, geometry));
            for (int group = 0; group < 8; group++)
            {
                int face = cursor + 4;
                int[] v = group is 2 or 6 ? [11, 13, 17] : [13, 17, 21, 23];
                if (group is 2 or 3 or 6 or 7)
                {
                    int[] added = v.Select(offset => source.Data[face + offset] + 2).ToArray();
                    bool overflow = added.Any(value => value > 255);
                    for (int corner = 0; corner < v.Length; corner++)
                    {
                        expected[face + v[corner]] = unchecked((byte)(added[corner] - (overflow ? 128 : 0)));
                    }
                    if (group == 2)
                    {
                        Assert.Equal(new byte[] { 127, 128, 130 }, v.Select(offset => expected[face + offset]).ToArray());
                    }
                    if (group == 6)
                    {
                        Assert.Equal(new byte[] { 2, 3, 255 }, v.Select(offset => expected[face + offset]).ToArray());
                    }
                }
                cursor += 4 + Stride(group);
            }
        }
        NativeBattleGfAnimationSource next = NativeBattleMoogle96ProgramDrawing.MutateUv7340A0(source);
        Assert.Equal(source.Address, next.Address);
        Assert.Equal(expected, next.Data.ToArray());
        Assert.NotEqual(source.Data.ToArray(), next.Data.ToArray());
    }

    private static NativeBattleGfAnimationNode Node(uint header = 0, NativeBattleVector? position = null, NativeBattleVector? angles = null, NativeBattleVector? scale = null)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Put(8, position ?? new(0, 0, 0));
        Put(16, angles ?? new(0, 0, 0));
        Put(24, scale ?? new(4096, 4096, 4096));
        return new(0, 0, bytes.ToImmutableArray());

        void Put(int offset, NativeBattleVector vector)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), vector.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), vector.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), vector.Z);
        }
    }

    private static NativeBattleGfAnimationSource Source()
    {
        const int geometryBytes = 252;
        const int prefix = 8 + 21 * 4;
        byte[] bytes = Enumerable.Repeat((byte)0xA5, prefix + 21 * geometryBytes).ToArray();
        Put32(0, 21);
        Put32(4, (uint)bytes.Length);
        for (int index = 0; index < 21; index++)
        {
            int geometry = prefix + index * geometryBytes;
            Put32(8 + index * 4, (uint)geometry);
            Put32(geometry, 44);
            Put32(geometry + 4, 4);
            Put32(geometry + 8, 1);
            int cursor = geometry + 44;
            for (int group = 0; group < 8; group++)
            {
                Put32(cursor, 1);
                int face = cursor + 4;
                int[] offsets = group is 2 or 6 ? [11, 13, 17] : [13, 17, 21, 23];
                if (group is 2 or 3 or 6 or 7)
                {
                    byte[] values = group == 6 ? [0, 1, 253] : [253, 254, 0, 127];
                    for (int corner = 0; corner < offsets.Length; corner++)
                    {
                        bytes[face + offsets[corner]] = values[corner];
                    }
                }
                cursor += 4 + Stride(group);
            }
        }
        return new(0x1000, bytes.ToImmutableArray());

        void Put32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }

    private static int Stride(int group) => group switch { 0 or 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
    private static uint U32(ImmutableArray<byte> source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan().Slice(offset));
}
