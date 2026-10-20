using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullCure280Tests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 1792)]
    [InlineData(8, 2048)]
    [InlineData(57, 2048)]
    [InlineData(58, 1792)]
    [InlineData(64, 256)]
    [InlineData(65, 0)]
    public void Original_director_flash_retains_its_two_eight_frame_ramps(short counter, int expected)
    {
        Assert.Equal(expected, NativeBattleFullCure280Geometry.Flash(counter));
    }

    [Fact]
    public void Ordinary_offsets_wrap_to_words_before_camera_rotation()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 100);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix actual = NativeBattleFullCure280Geometry.Compose(node, view, NativeBattleFullCure280State.FromColdProcess().Billboard, new(32760, 0, 0));
        Assert.Equal(unchecked((short)(32760 + 100)) + 10, actual.X);
        Assert.Equal(20, actual.Y);
        Assert.Equal(30, actual.Z);
    }
}
