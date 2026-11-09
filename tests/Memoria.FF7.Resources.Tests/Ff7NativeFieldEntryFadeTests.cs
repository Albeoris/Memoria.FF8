using System.Security.Cryptography;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7NativeFieldEntryFadeTests
{
    [Fact]
    public void Original_entry_alpha_uses_the_authenticated_bounded_immutable_table()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF7_GAME_DIR for original native field-entry alpha data.");
        IReadOnlyList<byte> alpha = Ff7PcExecutableData.Load(Ff7Installation.Discover(directory)).NativeFieldEntryFadeAlpha;
        Assert.Equal(257, alpha.Count);
        Assert.Equal((byte)0, alpha[0]);
        Assert.Equal((byte)64, alpha[64]);
        Assert.Equal((byte)168, alpha[128]);
        Assert.Equal((byte)242, alpha[192]);
        Assert.Equal((byte)254, alpha[240]);
        Assert.Equal((byte)255, alpha[256]);
        Assert.True(alpha.Zip(alpha.Skip(1)).All(pair => pair.First <= pair.Second));
        Assert.Equal("54DAA5E56A90AB7A6C5D15F3C3BAFE977F14D1919731F3CC34E995956486B7B7", Convert.ToHexString(SHA256.HashData(alpha.ToArray())));
        Assert.Throws<ArgumentOutOfRangeException>(() => alpha[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => alpha[257]);
        Assert.Throws<NotSupportedException>(() => ((IList<byte>)alpha)[0] = 255);
        Assert.Equal((byte)0, alpha[0]);
    }
}
