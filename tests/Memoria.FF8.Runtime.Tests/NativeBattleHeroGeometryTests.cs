using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHeroGeometryTests
{
    [Fact]
    public void Scale_operand_controls_native_arithmetic_shift_and_matrix_scale_independently_of_visibility()
    {
        NativeBattleGfAnimationNode node = Node(0x200, new(-1, 3, -3), new(4096, 4096, 4096));
        NativeBattleHeroDrawContext context = new(new(100, 200, 300), null, -2048, 2048, -2048, true, 0, 0);
        NativeBattleActionMatrix scaled = NativeBattleHeroGeometry.Compose(node, context, View());
        Assert.Equal(new short[] { -2048, 0, 0, 0, 2048, 0, 0, 0, -2048 }, scaled.Rotation);
        Assert.Equal((110, 221, 331), (scaled.X, scaled.Y, scaled.Z));
        NativeBattleActionMatrix unscaled = NativeBattleHeroGeometry.Compose(node, context with { ScaleEnabled = false }, View());
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, unscaled.Rotation);
        Assert.Equal((109, 223, 327), (unscaled.X, unscaled.Y, unscaled.Z));
        Assert.Throws<NotSupportedException>(() => NativeBattleHeroGeometry.Compose(node, context with { ScaleEnabled = null }, View()));
    }

    [Fact]
    public void World_branch_WORD_wraps_anchor_plus_offset_while_header200_adds_offset_after_rotation()
    {
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleHeroDrawContext context = new(new(32760, 0, 0), null, 4096, 4096, 4096, false, 0, 0);
        NativeBattleActionMatrix world = NativeBattleHeroGeometry.Compose(Node(0, new(20, 0, 0), new(4096, 4096, 4096)), context, view);
        NativeBattleActionMatrix direct = NativeBattleHeroGeometry.Compose(Node(0x200, new(20, 0, 0), new(4096, 4096, 4096)), context, view);
        Assert.Equal((10, -32736, 30), (world.X, world.Y, world.Z));
        Assert.Equal((30, 32780, 30), (direct.X, direct.Y, direct.Z));
        Assert.Equal(view.Rotation.ToArray(), world.Rotation.ToArray());
        Assert.Equal(View().Rotation.ToArray(), direct.Rotation.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x100)]
    public void Node_scale_is_applied_after_view_composition_and_preserves_translation(uint header)
    {
        NativeBattleHeroDrawContext context = new(new(100, 200, 300), 123, 4096, 4096, 4096, false, -128, 32);
        NativeBattleActionMatrix result = NativeBattleHeroGeometry.Compose(Node(header, new(0, 0, 0), new(0, 4096, -4096)), context, View());
        Assert.Equal(new short[] { 0, 0, 0, 0, 4096, 0, 0, 0, -4096 }, result.Rotation);
        Assert.Equal((110, 220, 330), (result.X, result.Y, result.Z));
    }

    [Fact]
    public void Hidden_nodes_require_neither_scale_owner_nor_a_temporary_allocation()
    {
        Assert.True(NativeBattleHeroGeometry.IsHidden(Node(0, new(0, 0, 0), new(0, 0, 0))));
        Assert.True(NativeBattleHeroGeometry.IsHidden(Node(0, new(0, 0, 0), new(4096, 4096, 4096), fade: 4096)));
        Assert.False(NativeBattleHeroGeometry.IsHidden(Node(0, new(0, 0, 0), new(4096, 4096, 4096), fade: 4096, color: 1)));
        Assert.False(NativeBattleHeroGeometry.IsHidden(Node(0, new(0, 0, 0), new(4096, 4096, 4096), fade: -1)));
    }

    [Fact]
    public void Shape_endpoints_read_original_rows_and_morph_retains_actual_destination_padding()
    {
        byte[] source = new byte[80];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 2);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(28), 0);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(36), 6000);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(44), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(52), 7000);
        NativeBattleGfAnimationSource resource = new(0x1000, source.ToImmutableArray());
        NativeBattleGfAnimationNode first = Node(0, new(0, 0, 0), new(4096, 4096, 4096), firstShape: 0, secondShape: 1);
        Assert.Equal(28, NativeBattleHeroGeometry.Select(resource, first).SourceOffset);
        Assert.Equal(44, NativeBattleHeroGeometry.Select(resource, Node(0, new(0, 0, 0), new(4096, 4096, 4096), ratio: 4096, firstShape: 0, secondShape: 1)).SourceOffset);
        NativeBattleHeroVertexSelection selection = NativeBattleHeroGeometry.Select(resource, Node(0, new(0, 0, 0), new(4096, 4096, 4096), ratio: 2048, firstShape: 0, secondShape: 1));
        Assert.True(selection.RequiresMorph);
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 16).ToImmutableArray();
        ImmutableArray<byte> result = NativeBattleHeroGeometry.SelectVertices(resource, selection, scratch);
        Assert.Equal((short)2048, BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan()));
        Assert.Equal((short)6500, BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan().Slice(8)));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, new[] { result[6], result[7], result[14], result[15] });
        Assert.Throws<InvalidDataException>(() => NativeBattleHeroGeometry.SelectVertices(resource, selection, default));
    }

    [Fact]
    public void Fan_composes_actual_anchor_then_view_and_keeps_signed_radius()
    {
        NativeBattleHeroFan fan = new(new(0, 0, 0), new(100, -200, 300, 1234), -2048, 400, 256, 0x1025528, 0);
        NativeBattleActionMatrix result = NativeBattleHeroGeometry.FanMatrix(fan, View());
        Assert.Equal(new short[] { -2048, 0, 0, 0, -2048, 0, 0, 0, -2048 }, result.Rotation);
        Assert.Equal((110, -180, 330), (result.X, result.Y, result.Z));
    }

    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector offset, NativeBattleVector scale, short fade = 0, uint color = 0, short ratio = 0, short firstShape = 0, short secondShape = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Write(8, offset);
        Write(24, scale);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), color);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), fade);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), firstShape);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), secondShape);
        return new(0, 0, bytes.ToImmutableArray());

        void Write(int start, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start + 4), value.Z);
        }
    }
}
