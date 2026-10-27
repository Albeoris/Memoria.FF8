using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco98ProgramDrawingTests
{
    [Theory]
    [InlineData(0x4AC)]
    [InlineData(0x44C)]
    [InlineData(0x170)]
    [InlineData(0x494)]
    [InlineData(0x7D0)]
    public void Caller_capacity_is_not_its_clear_operand_and_97_clear_is_not_an_98_fallback(int clear)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        NativeBattleGfAnimationSource source = new(0x1000, bytes.ToImmutableArray());
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x722460, Enumerable.Repeat((byte)0xA5, 0x864 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)clear);
        NativeBattleModulePoolTask next = NativeBattleChoco98ProgramDrawing.InitializeWorkspace7224C0(task, source, [1, 0, 2, 0, 3, 0, 4, 0]);
        Assert.Equal(0x864 - 12, next.PayloadLength);
        ImmutableArray<byte> payload = NativeBattleStorageSnapshots.Capture(next).Payload;
        Assert.All(payload.Skip(0x94 - 12 + 4).Take(clear - 4), value => Assert.Equal((byte)0, value));
        Assert.All(payload.Skip(0x94 - 12 + clear), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((ushort)2, next.ReadWord(0x1E));
        Assert.Equal((ushort)4, next.ReadWord(0x22));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco98ProgramDrawing.InitializeWorkspace7224C0(task.WriteDword(0x78, 0x290), source, new byte[8].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task, source, new byte[8].ToImmutableArray()));
    }

    [Fact]
    public void Large_single_shape_uses_its_source_vertices_while_partial_morph_requires_the_distinct_168_owner()
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleSummonColors colors = NativeBattleGteColorInterpolation.CodeConstants45F270;
        byte[] node = new byte[44];
        for (int offset = 24; offset <= 28; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(offset), 4096);
        }
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x168).ToImmutableArray();
        NativeBattleChoco97NodeDraw direct = NativeBattleChoco98ProgramDrawing.Node722640(Source(120, 1), new(0, 0, node.ToImmutableArray()), scratch, matrix, projection, colors);
        Assert.Equal(scratch.ToArray(), direct.MorphScratch.ToArray());
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        NativeBattleGfAnimationNode partial = new(0, 0, node.ToImmutableArray());
        NativeBattleChoco97NodeDraw morphed = NativeBattleChoco98ProgramDrawing.Node722640(Source(1, 2), partial, scratch, matrix, projection, colors);
        Assert.All(morphed.MorphScratch.Skip(6), value => Assert.Equal((byte)0xA5, value));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98ProgramDrawing.Node722640(Source(1, 2), partial, new byte[0x3C0].ToImmutableArray(), matrix, projection, colors));
    }

    private static NativeBattleGfAnimationSource Source(int count, int shapes)
    {
        int faces = 12 + count * shapes * 8;
        byte[] bytes = new byte[12 + faces + 32];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), shapes);
        return new(0x1000, bytes.ToImmutableArray());
    }
}
