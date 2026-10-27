using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTextSpeedControlTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(255)]
    public void SpeedControlRoundTripsAsAParameterizedTag(int parameter)
    {
        FF8Encoding encoding = FF8Encoding.CreateEuropean(FF8TextLanguage.EN);
        byte[] bytes = [8, (byte)parameter];
        string decoded = encoding.GetString(bytes);
        Assert.Equal($"{{Speed {parameter}}}", decoded);
        Assert.Equal(bytes, encoding.GetBytes(decoded));
    }

    [Fact]
    public void SpeedControlRequiresItsParameterAndUnknownControlsStillFail()
    {
        FF8Encoding encoding = FF8Encoding.CreateEuropean(FF8TextLanguage.EN);
        Assert.Throws<FormatException>(() => encoding.GetString([8]));
        Assert.Throws<FormatException>(() => encoding.GetString([16, 0]));
    }

    [Fact]
    public void SpeedChangesAreNonvisualAndZeroEndsTheMessage()
    {
        Assert.Equal("AB", DialogueTextFormatter.Format("A{Speed 32}{Speed 33}{Speed 34}B"));
        Assert.Equal("", DialogueTextFormatter.Format(FF8Encoding.CreateEuropean(FF8TextLanguage.EN).GetString([8, 0])));
        Assert.Equal("Before", DialogueTextFormatter.Format("Before{Speed 0}After{Page}Hidden"));
        Assert.Throws<NotSupportedException>(() => DialogueTextFormatter.Format("{Speed 1}Unsupported"));
        Assert.Equal("{Unknown 8}", DialogueTextFormatter.Format("{Unknown 8}"));
    }
}
