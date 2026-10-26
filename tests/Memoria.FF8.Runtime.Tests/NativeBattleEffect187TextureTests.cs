using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187TextureTests
{
    [Theory]
    [InlineData(0, 127)]
    [InlineData(1024, 1)]
    [InlineData(2048, 127)]
    [InlineData(3072, 125)]
    public void RowZeroUsesSignedVerticalDisplacementAndWrap(short phase, int expectedSourceRow)
    {
        byte[] source = Enumerable.Range(0, 128 * 128).Select(index => (byte)(index / 128)).ToArray();
        byte[] actual = NativeBattleEffect187Texture.Warp648490(source, phase);
        Assert.All(actual.Take(128), value => Assert.Equal((byte)expectedSourceRow, value));
    }

    [Fact]
    public void PositiveAmplitudeWrapsColumnsAndPreservesTheSource()
    {
        byte[] source = Enumerable.Range(0, 128 * 128).Select(index => (byte)(index % 128)).ToArray();
        byte[] retained = source.ToArray();
        byte[] actual = NativeBattleEffect187Texture.Warp648490(source, 1024);
        Assert.Equal((byte)116, actual[64 * 128]);
        Assert.Equal((byte)127, actual[64 * 128 + 11]);
        Assert.Equal((byte)0, actual[64 * 128 + 12]);
        Assert.Equal(retained, source);
    }

    [Fact]
    public void AnIncompleteSourceIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect187Texture.Warp648490(new byte[128 * 128 - 1], 0));
    }
}
