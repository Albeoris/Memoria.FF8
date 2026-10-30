using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSavedWorldAngleTests
{
    [Fact]
    public void EquivalentOperationalAnglesRetainTheOriginalFullWords()
    {
        NativeSavedWorldAngleWords words = new(Enumerable.Repeat((ushort)0xF456, 8).ToImmutableArray(), 0xF9C5);
        Assert.Equal((ushort)0x456, NativeSavedWorldAngleWords.Normalize(0xF456));
        Assert.Equal((ushort)0x9C5, NativeSavedWorldAngleWords.Normalize(0xF9C5));
        Assert.Equal((ushort)0xF456, words.ResolveBodyWord(4, 0x456));
        Assert.Equal((ushort)0xF9C5, words.ResolveCameraWord(0x9C5));
        Assert.Equal((ushort)123, words.ResolveBodyWord(4, 123));
        Assert.Equal((ushort)321, words.ResolveCameraWord(321));
    }
}
