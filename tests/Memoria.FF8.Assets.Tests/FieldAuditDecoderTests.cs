using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldAuditDecoderTests
{
    [Fact]
    public void FieldSpecificJapaneseTablesStayIndependentUnderParallelDecoding()
    {
        string[] fields = ["testno", "start"];
        DefaultJapaneseFieldCharacters provider = new();
        Assert.NotEqual(provider.Get(fields[0]).Characters[0], provider.Get(fields[1]).Characters[0]);
        Parallel.For(0, 40, index =>
        {
            string field = fields[index % fields.Length];
            FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(Bank([0x1C, 0x20, 0]), "ja", field);
            Assert.True(bank.Complete);
            Assert.Equal(provider.Get(field).Characters[0] + "{End}", Assert.Single(bank.Rows).Text);
            Assert.NotNull(bank.CharacterTableSha256);
        });
    }

    [Fact]
    public void JapaneseFieldBlankGlyphRemainsAValidCharacter()
    {
        DefaultJapaneseFieldCharacters provider = new();
        Assert.Equal("様腹 ", provider.Get("tgview1").Characters);
        Assert.Equal(new byte[] { 0x1C, 0x22 }, FF8Encoding.CreateJapanese(provider, "tgview1").GetBytes(" "));
        FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(Bank([0x1C, 0x22, 0]), "ja", "tgview1");
        Assert.True(bank.Complete);
        Assert.Equal(" {End}", Assert.Single(bank.Rows).Text);
        Assert.NotNull(bank.CharacterTableSha256);

        FieldAuditDecodedBank invalid = FieldAuditDecoder.Decode(Bank([0x1C, 0x23, 0]), "ja", "tgview1");
        Assert.False(invalid.Complete);
        Assert.NotNull(Assert.Single(invalid.Rows).Error);
    }

    [Theory]
    [InlineData("unknown-field", 0x20)]
    [InlineData("start", 0xFF)]
    [InlineData("start", 0x1F)]
    public void MissingAndInvalidJapaneseCharactersAreErrorsRatherThanPlaceholders(string field, byte index)
    {
        FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(Bank([0x1C, index]), "ja", field);
        Assert.False(bank.Complete);
        Assert.NotNull(Assert.Single(bank.Rows).Error);
        Assert.Null(bank.Rows[0].Text);
    }

    [Theory]
    [InlineData(0x19)]
    [InlineData(0x1C)]
    [InlineData(0x03)]
    [InlineData(0x09)]
    public void TruncatedEscapesAndControlsAreDiagnosedAtTheOriginalRow(byte value)
    {
        FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(Bank([value]), "ja", "start");
        Assert.False(bank.Complete);
        Assert.Contains("Truncated", bank.Rows[0].Error);
        Assert.Equal(4, bank.Rows[0].Offset);
    }

    [Fact]
    public void TagParametersAreNotMistakenForJapaneseEscapes()
    {
        FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(Bank([0x09, 0x1C, 0]), "ja", "unknown-field");
        Assert.True(bank.Complete);
        Assert.Equal("{Pause 28}{End}", bank.Rows[0].Text);
        Assert.Equal(["{Pause 28}", "{End}"], bank.Rows[0].Tokens);
    }

    [Fact]
    public void EmptyAndRepeatedOffsetsPreserveEveryOrdinal()
    {
        byte[] bank = new byte[13];
        BinaryPrimitives.WriteInt32LittleEndian(bank, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(8), 12);
        FieldAuditDecodedBank result = FieldAuditDecoder.Decode(bank, "en", "fixture");
        Assert.True(result.Complete);
        Assert.Equal([0, 1, 2], result.Rows.Select(row => row.Ordinal));
        Assert.Equal(["", "", "{End}"], result.Rows.Select(row => row.Text));
    }

    private static byte[] Bank(byte[] text)
    {
        byte[] result = new byte[4 + text.Length];
        BinaryPrimitives.WriteInt32LittleEndian(result, 4);
        text.CopyTo(result, 4);
        return result;
    }
}
