using Albeoris.Games.FF8.TextEncoding;
using Albeoris.Games.FF8.TextEncoding.Tags;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTextLanguageTests
{
    [Fact]
    public void MissingOrInvalidLanguageFailsForBothCountingAndConversion()
    {
        foreach (FF8Encoding encoding in new[] { FF8Encoding.CreateEuropean(), FF8Encoding.CreateJapanese(), FF8Encoding.CreateRussian() })
        {
            Assert.Throws<InvalidOperationException>(() => encoding.GetString([0x0E, 0x34]));
            Assert.Throws<InvalidOperationException>(() => encoding.GetCharCount([0x40]));
            Assert.Throws<InvalidOperationException>(() => encoding.GetBytes("test"));
            Assert.Throws<InvalidOperationException>(() => encoding.GetByteCount("test"));
            encoding.Language = (FF8TextLanguage)255;
            Assert.Throws<InvalidOperationException>(() => encoding.GetString([0x40]));
        }
    }

    [Fact]
    public void EachDictionaryUsesItsOwnEnumAndAllOperandsRoundTrip()
    {
        foreach (FF8TextLanguage language in Enum.GetValues<FF8TextLanguage>())
        {
            FF8Encoding encoding = NativeTextEncoding.Create(language.ToString());
            foreach (int value in Enumerable.Range(0, 256))
            {
                byte[] bytes = [0x0E, (byte)value];
                string tag = encoding.GetString(bytes);
                Assert.StartsWith("{Term" + language + " ", tag);
                Assert.Equal(bytes, encoding.GetBytes(tag));
                Assert.Equal(tag.Length, encoding.GetCharCount(bytes));
            }
        }
        FF8Encoding shared = FF8Encoding.CreateEuropean(FF8TextLanguage.EN);
        Assert.Equal("{TermEN Status}", shared.GetString([0x0E, 0x34]));
        shared.Language = FF8TextLanguage.DE;
        Assert.Equal("{TermDE AttackOneEnemy}", shared.GetString([0x0E, 0x34]));
        Assert.Throws<FormatException>(() => shared.GetBytes("{TermEN Status}"));
        Assert.Throws<FormatException>(() => shared.GetBytes("{Term Status}"));
    }

    [Fact]
    public void NonDictionaryEnumSelectorsRemainIndependentOfLanguage()
    {
        byte[][] tags = Enum.GetValues<TextTagCharacter>().Select(value => new byte[] { 3, (byte)value })
            .Concat(Enum.GetValues<TextTagKey>().Select(value => new byte[] { 5, (byte)value }))
            .Concat(Enum.GetValues<TextTagColor>().Select(value => new byte[] { 6, (byte)value }))
            .Concat(Enum.GetValues<TextTagDialog>().Select(value => new byte[] { 10, (byte)value }))
            .Concat(Enum.GetValues<TextTagName>().Select(value => new byte[] { 12, (byte)value })).ToArray();
        FF8Encoding english = NativeTextEncoding.Create("en");
        foreach (FF8TextLanguage language in Enum.GetValues<FF8TextLanguage>())
        {
            FF8Encoding encoding = NativeTextEncoding.Create(language.ToString());
            foreach (byte[] bytes in tags)
            {
                string text = encoding.GetString(bytes);
                Assert.Equal(english.GetString(bytes), text);
                Assert.Equal(bytes, encoding.GetBytes(text));
            }
        }
    }
}
