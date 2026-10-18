using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99ProgramDrawingTests
{
    [Theory]
    [InlineData(0x4AC)]
    [InlineData(0x44C)]
    [InlineData(0x170)]
    [InlineData(0xB0)]
    [InlineData(0x2C)]
    [InlineData(0x194)]
    [InlineData(0x30)]
    [InlineData(0x1E8)]
    public void Caller_capacity_is_not_its_clear_operand_and_other_profiles_are_not_a_fallback(int clear)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 8);
        NativeBattleGfAnimationSource source = new(0x1000, bytes.ToImmutableArray());
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x7189B0, Enumerable.Repeat((byte)0xA5, 0x548 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)clear);
        NativeBattleModulePoolTask next = NativeBattleChoco99ProgramDrawing.InitializeWorkspace(task, source, [1, 0, 2, 0, 3, 0, 4, 0]);
        Assert.Equal(0x548 - 12, next.Payload.Length);
        Assert.All(next.Payload.Skip(0x94 - 12 + 4).Take(clear - 4), value => Assert.Equal((byte)0, value));
        Assert.All(next.Payload.Skip(0x94 - 12 + clear), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((ushort)2, next.ReadWord(0x1E));
        Assert.Equal((ushort)4, next.ReadWord(0x22));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco99ProgramDrawing.InitializeWorkspace(task.WriteDword(0x78, 0x290), source, new byte[8].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task, source, new byte[8].ToImmutableArray()));
    }

    [Fact]
    public void Largest_partial_morph_uses_760_bytes_and_preserves_each_fourth_word()
    {
        const int count = 236;
        int faces = 12 + count * 2 * 8;
        byte[] bytes = new byte[12 + faces + 32];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 2);
        byte[] node = new byte[44];
        for (int offset = 24; offset <= 28; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleCameraProjection projection = new(matrix, 216, 160, 108);
        NativeBattleGfAnimationSource source = new(0x1000, bytes.ToImmutableArray());
        NativeBattleGfAnimationNode partial = new(0, 0, node.ToImmutableArray());
        NativeBattleChoco97NodeDraw draw = NativeBattleChoco99ProgramDrawing.Node718B90(source, partial, Enumerable.Repeat((byte)0xA5, 0x760).ToImmutableArray(), matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        for (int index = 0; index < count; index++)
        {
            Assert.All(draw.MorphScratch.Skip(index * 8).Take(6), value => Assert.Equal((byte)0, value));
            Assert.Equal((byte)0xA5, draw.MorphScratch[index * 8 + 6]);
            Assert.Equal((byte)0xA5, draw.MorphScratch[index * 8 + 7]);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99ProgramDrawing.Node718B90(source, partial, new byte[0x3C0].ToImmutableArray(), matrix, projection, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    [Fact]
    public void Pending_loaded_callers_are_not_admitted_by_a_numeric_profile()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99ProgramDrawing.LoadedProfile((NativeBattleChoco99LoadedProgram)475));
    }
}
