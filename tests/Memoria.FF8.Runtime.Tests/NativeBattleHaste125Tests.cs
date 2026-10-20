using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHaste125Tests
{
    [Fact]
    public void Scale_is_explicit_even_when_effect_camera_has_zero_off_diagonal()
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix result = NativeBattleHaste125Geometry.Compose(Node(0), identity with { X = 100 }, identity, 2048);
        Assert.Equal(200, result.X);
        Assert.Equal((short)2048, result.Rotation[0]);
    }

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(0x200, 200, 0)]
    [InlineData(0x1000, 200, 0)]
    public void Direct_and_billboard_offsets_keep_their_distinct_basis(uint flags, int x, int y)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix turned = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 100, 0, 0, 4096);
        NativeBattleActionMatrix result = NativeBattleHaste125Geometry.Compose(Node(flags), turned, identity, 2048);
        Assert.Equal(x, result.X);
        Assert.Equal(y, result.Y);
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
