using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRush91ProgramDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static ImmutableArray<byte> Scratch => Enumerable.Repeat((byte)0xA5, 0x5A0).ToImmutableArray();

    [Fact]
    public void Non_direct_node_rotates_position_to_full_MAC_then_applies_column_scale_after_basis()
    {
        byte[] data = Node().Data.ToArray();
        Put(8, 30000);
        Put(10, 20000);
        Put(20, 1024);
        Put(24, 2048);
        Put(28, 8192);
        NativeBattleGfAnimationNode node = Node() with { Data = data.ToImmutableArray() };
        NativeBattleActionMatrix context = View with { Rotation = [8192, 4096, 0, 0, 4096, 0, 0, 0, 4096], X = 100, Y = 200 };
        NativeBattleActionMatrix matrix = NativeBattleRush91ProgramDrawing.ComposeNodeMatrix(node, context);
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Multiply56C090(context, NativeBattleGfMatrices.Euler701310(node.Vector16)), 2048, 4096, 8192);
        Assert.Equal(expected.Rotation.ToArray(), matrix.Rotation.ToArray());
        Assert.Equal((80100, 20200, 4096), (matrix.X, matrix.Y, matrix.Z));

        void Put(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), value);
    }

    [Fact]
    public void Morph_preserves_actual_fourth_words_and_unselected_scratch_tail_while_endpoints_do_not_write()
    {
        NativeBattleGfAnimationNode node = Node();
        NativeBattleRush91NodeDraw morphed = NativeBattleRush91ProgramDrawing.Node761CE0(Source(), node, Scratch, View, 0, Projection, Colors);
        Assert.Equal(36, morphed.Packets.AllocationBytes);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal((ushort)0xA5A5, BinaryPrimitives.ReadUInt16LittleEndian(morphed.MorphScratch.AsSpan().Slice(vertex * 8 + 6)));
            Assert.Equal((short)((vertex & 1) == 0 ? -5 : 15), BinaryPrimitives.ReadInt16LittleEndian(morphed.MorphScratch.AsSpan().Slice(vertex * 8)));
        }
        Assert.Equal(Scratch.AsSpan().Slice(32).ToArray(), morphed.MorphScratch.AsSpan().Slice(32).ToArray());
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), 4096);
        NativeBattleRush91NodeDraw endpoint = NativeBattleRush91ProgramDrawing.Node761CE0(Source(), node with { Data = data.ToImmutableArray() }, Scratch, View, 0, Projection, Colors);
        Assert.Equal(Scratch.ToArray(), endpoint.MorphScratch.ToArray());
        NativeBattleRush91NodeDraw biased = NativeBattleRush91ProgramDrawing.Node761CE0(Source(), node, Scratch, View, -256, Projection, Colors);
        Assert.Equal(morphed.Packets.Attempts[0].Primitive!.Bucket - 64, biased.Packets.Attempts[0].Primitive!.Bucket);
    }

    [Fact]
    public void Skip_gates_preserve_scratch_and_invalid_actual_owners_fail_explicitly()
    {
        byte[] data = Node().Data.ToArray();
        data.AsSpan(24, 6).Clear();
        NativeBattleRush91NodeDraw skipped = NativeBattleRush91ProgramDrawing.Node761CE0(new(0, []), Node() with { Data = data.ToImmutableArray() }, Scratch, View, 0, Projection, Colors);
        Assert.Empty(skipped.Packets.Attempts);
        Assert.Equal(Scratch.ToArray(), skipped.MorphScratch.ToArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleRush91ProgramDrawing.Node761CE0(Source(), Node(), default, View, 0, Projection, Colors));
        data = Node().Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 3);
        Assert.Throws<InvalidDataException>(() => NativeBattleRush91ProgramDrawing.Node761CE0(Source(), Node() with { Data = data.ToImmutableArray() }, Scratch, View, 0, Projection, Colors));
    }

    private static NativeBattleGfAnimationNode Node()
    {
        byte[] data = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        return new(0, 0x3010, data.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[184];
        Put32(0, 1);
        Put32(4, (uint)bytes.Length);
        Put32(8, 12);
        Put32(12, 108);
        Put32(16, 4);
        Put32(20, 3);
        for (int shape = 0; shape < 3; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int offset = 24 + shape * 32 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)(((vertex & 1) == 0 ? -10 : 10) + shape * 10));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(vertex < 2 ? -10 : 10));
            }
        }
        Put32(140, 1);
        Put32(144, 0x38808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(150), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(152), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(154), 6);
        Put32(156, 0x00102030);
        Put32(160, 0x00405060);
        Put32(164, 0x00708090);
        return new(0x1000, bytes.ToImmutableArray());

        void Put32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
