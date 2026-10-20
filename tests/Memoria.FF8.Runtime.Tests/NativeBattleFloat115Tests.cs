using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFloat115Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0x200)]
    [InlineData(0x1000)]
    public void Glyph_offset_always_uses_its_supplied_context(uint flags)
    {
        NativeBattleActionMatrix context = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 0, 0, 4096);
        NativeBattleActionMatrix result = NativeBattleFloat115Geometry.Compose(Node(flags), context, 2048);
        Assert.Equal(100, result.X);
        Assert.Equal(100, result.Y);
        Assert.Equal((short)2048, result.Rotation[3]);
    }

    [Fact]
    public void Upper_depth_uses_rotated_word_and_size_without_translation()
    {
        NativeBattleActionMatrix context = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 100000 };
        Assert.Equal(128, NativeBattleFloat115Geometry.UpperDepth(context, new(0, 0, 1024), 2048));
        Assert.Equal(-128, NativeBattleFloat115Geometry.UpperDepth(context, new(0, 0, -1024), 2048));
    }

    private static NativeBattleGfAnimationNode Node(uint flags)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 200);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
