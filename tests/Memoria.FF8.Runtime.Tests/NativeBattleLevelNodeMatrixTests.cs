using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLevelNodeMatrixTests
{
    [Theory]
    [InlineData(0u, -7, 32, 37)]
    [InlineData(0x200u, 3, 22, 37)]
    [InlineData(0x1000u, 3, 22, 37)]
    public void Matrix_branches_preserve_native_world_or_camera_relative_translation(uint flags, int x, int y, int z)
    {
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 1, 2, 3, 4096);
        NativeBattleActionMatrix billboard = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix actual = NativeBattleLevelNodeMatrix.Compose(Node(flags, new(10, 0, 0)), new(20, 8, 34), new(4096, 4096, 4096), true, view, billboard);
        Assert.Equal((x, y, z), (actual.X, actual.Y, actual.Z));
        Assert.Equal(flags == 0 ? view.Rotation : billboard.Rotation, actual.Rotation);
    }

    [Fact]
    public void Scaled_translation_wraps_to_signed_WORD_before_the_world_branch_addition()
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix actual = NativeBattleLevelNodeMatrix.Compose(Node(0, new(30000, -3, 0)), new(10000, 0, 0), new(8192, 2048, 4096), true, identity, identity);
        Assert.Equal(unchecked((short)(unchecked((short)60000) + 10000)), actual.X);
        Assert.Equal(-2, actual.Y);
    }

    [Theory]
    [InlineData(10, 4, 20, 20, 4, 10)]
    [InlineData(20, 4, 10, 20, 4, 10)]
    [InlineData(10, 30, 20, 30, 30, 30)]
    public void Body_extents_follow_the_native_XZ_swap_and_height_dominance(int x, int y, int z, int expectedX, int expectedY, int expectedZ)
    {
        Assert.Equal(new NativeBattleLevelScale(expectedX, expectedY, expectedZ), NativeBattleLevelNodeMatrix.BoundsScale(new(0, 0, 0), new((short)x, (short)y, (short)z)));
    }

    private static NativeBattleGfAnimationNode Node(uint flags, NativeBattleVector offset)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), flags);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), offset.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), offset.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), offset.Z);
        for (int lane = 0; lane < 3; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + lane * 2), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
}
