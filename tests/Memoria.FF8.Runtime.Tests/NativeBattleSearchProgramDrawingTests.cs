using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSearchProgramDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Direct_node_ignores_context_rotation_but_applies_column_scale_after_its_own_Euler()
    {
        byte[] data = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 30);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), 1024);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28), 8192);
        NativeBattleGfAnimationNode node = Node() with { Data = data.ToImmutableArray() };
        NativeBattleActionMatrix context = View with { Rotation = [0, -4096, 0, 4096, 0, 0, 0, 0, 4096], X = 100, Y = 200 };
        NativeBattleActionMatrix matrix = NativeBattleSearchProgramDrawing.ComposeNodeMatrix(node, context);
        Assert.Equal(new short[] { 0, 4096, 0, -2048, 0, 0, 0, 0, 8192 }, matrix.Rotation.ToArray());
        Assert.Equal((110, 220, 4126), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void Task_context_uses_postmultiplied_Y_then_X_then_Z_before_column_scales()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x62, 1024).WriteWord(0x60, 1024).WriteDword(0x50, 2048).WriteDword(0x58, 8192).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300);
        NativeBattleActionMatrix context = NativeBattleSearchProgramDrawing.ComposeContext758D70(task, View);
        Assert.Equal(new short[] { 0, 4096, 0, 0, 0, -8192, -2048, 0, 0 }, context.Rotation.ToArray());
        Assert.Equal((100, 200, 4396), (context.X, context.Y, context.Z));
    }

    [Fact]
    public void Twelve_byte_geometry_uses_the_proved_FT4_alias_with_actual_signed_depth_bias()
    {
        NativeBattleGfGeometryBatch normal = NativeBattleSearchProgramDrawing.Node758EA0(Source(), Node(), View, 0, Projection, Colors);
        NativeBattleGfGeometryBatch biased = NativeBattleSearchProgramDrawing.Node758EA0(Source(), Node(), View, -256, Projection, Colors);
        Assert.Equal(40, normal.AllocationBytes);
        NativeBattleGeometryPrimitive first = Assert.Single(normal.Attempts).Primitive!;
        NativeBattleGeometryPrimitive shifted = Assert.Single(biased.Attempts).Primitive!;
        Assert.Equal(0x2C808080u, first.Words[1]);
        Assert.Equal(273, first.Bucket);
        Assert.Equal(209, shifted.Bucket);
        Assert.Equal(first.Words.ToArray(), shifted.Words.ToArray());
        Assert.Equal(new[] { 4096, 4096, 4096, 4096 }, first.VertexPayload4.ToArray());
    }

    [Fact]
    public void Skip_gates_precede_geometry_but_unproved_position_or_shape_owners_fail_fast()
    {
        byte[] data = Node().Data.ToArray();
        data.AsSpan(24, 6).Clear();
        Assert.Empty(NativeBattleSearchProgramDrawing.Node758EA0(new(0, []), Node() with { Data = data.ToImmutableArray() }, View, 0, Projection, Colors).Attempts);
        data = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), 4096);
        Assert.Empty(NativeBattleSearchProgramDrawing.Node758EA0(new(0, []), Node() with { Data = data.ToImmutableArray() }, View, 0, Projection, Colors).Attempts);
        data = Node().Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleSearchProgramDrawing.Node758EA0(Source(), Node() with { Data = data.ToImmutableArray() }, View, 0, Projection, Colors));
        data = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleSearchProgramDrawing.Node758EA0(Source(), Node() with { Data = data.ToImmutableArray() }, View, 0, Projection, Colors));
        Assert.Throws<InvalidDataException>(() => NativeBattleSearchProgramDrawing.Advance758D70(Task().WriteDword(0x74, 0), Source(), View, Projection, Colors));
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 0, new byte[0x17C - 12].ToImmutableArray()).WriteDword(0x74, 0x1000).WriteDword(0x78, 0xE0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);

    private static NativeBattleGfAnimationNode Node()
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x200);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, data.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[112];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 112);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 44);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 1);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + vertex * 8), (short)((vertex & 1) == 0 ? -10 : 10));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26 + vertex * 8), (short)(vertex < 2 ? -10 : 10));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x2C808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(78), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(82), 6);
        return new(0x1000, bytes.ToImmutableArray());
    }
}
