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

    [Theory]
    [InlineData("bg2f_1a", "日忘信条勤室戻挨拶慣就節主君独")]
    [InlineData("bgmast_6", "営俺達")]
    [InlineData("bgroom_2", "日予昨考夕変週筆記習由準名表慣")]
    [InlineData("ewbrdg2", "予")]
    [InlineData("glstage2", "薄汚愚甘美幻想永")]
    public void NamedArchiveTablesDecodeExactIndicesAndRejectTheNextUndeclaredGlyph(string field, string characters)
    {
        DefaultJapaneseFieldCharacters provider = new();
        FieldCharacterSet table = provider.Get(field);
        Assert.Null(table.Id);
        Assert.Equal(characters, table.Characters);
        Assert.Same(table, provider.Get(field.ToUpperInvariant()));
        for (int index = 0; index < characters.Length; index++)
        {
            FieldAuditDecodedBank decoded = FieldAuditDecoder.Decode(Bank([0x1C, checked((byte)(0x20 + index)), 0]), "ja", field);
            Assert.True(decoded.Complete);
            Assert.Equal(characters[index] + "{End}", Assert.Single(decoded.Rows).Text);
        }
        FieldAuditDecodedBank unknown = FieldAuditDecoder.Decode(Bank([0x1C, checked((byte)(0x20 + characters.Length)), 0]), "ja", field);
        Assert.False(unknown.Complete);
        Assert.Null(Assert.Single(unknown.Rows).Text);
        Assert.Contains("Missing or invalid field-character table entry", unknown.Rows[0].Error);
    }

    [Fact]
    public void NamedOnlyArchiveTablesDoNotAcquireOrReplaceNumericFieldIdentities()
    {
        DefaultJapaneseFieldCharacters provider = new();
        Assert.Null(provider.TryGet(0));
        Assert.Null(provider.TryGet(-1));
        Assert.Equal(647, Enumerable.Range(0, ushort.MaxValue + 1).Count(id => provider.TryGet(id) is not null));
        Assert.Equal("testno", provider.Get(72).Name);
        Assert.Equal("tgview1", provider.Get(891).Name);
        Assert.Same(provider.Get("tgview1"), provider.Get(891));
    }

    [Fact]
    public void TestFieldNativeGlyph23DecodesAsItsActualIdeograph()
    {
        DefaultJapaneseFieldCharacters provider = new();
        Assert.Equal('慣', provider.Get("testno").Characters[23]);
        Assert.Equal(72, provider.Get("testno").Id);
        FieldAuditDecodedBank decoded = FieldAuditDecoder.Decode(Bank([0x1C, 0x37, 0]), "ja", "testno");
        Assert.True(decoded.Complete);
        Assert.Equal("慣{End}", Assert.Single(decoded.Rows).Text);
        Assert.False(FieldAuditDecoder.Decode(Bank([0x1C, 0x38, 0]), "ja", "testno").Complete);
    }

    [Theory]
    [InlineData("bg2f_1a", 14)]
    [InlineData("bgmast_6", 6)]
    [InlineData("bgroom_2", 11)]
    [InlineData("ewbrdg2", 1)]
    [InlineData("glstage2", 7)]
    public void OptionalOriginalArchivedJapaneseBanksDecodeWithoutUnknownGlyphSubstitution(string field, int rowCount)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, "main.zzz")), "Set MEMORIA_FF8_LOCALIZATION_GAME_DIR to optional original Remastered resources.");
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using var source = new Memoria.FF8.Assets.Remastered.RemasteredGameDataSource(directory, "ja");
        Memoria.FF8.Resources.ResourceContent? resource = source.Find(new("field/" + field + "/" + field + ".msd"));
        Assert.NotNull(resource);
        using Stream stream = resource.OpenRead();
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        FieldAuditDecodedBank bank = FieldAuditDecoder.Decode(bytes, "ja", field);
        Assert.Equal(rowCount, bank.Rows.Length);
        Assert.True(bank.Complete, string.Join("; ", bank.Rows.Where(row => row.Error is not null).Select(row => row.Ordinal + ": " + row.Error)));
        Assert.All(bank.Rows, row => Assert.NotNull(row.Text));
        string? reportDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, field + "-decoded.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Field = field,
                SourceSHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
                bank.Decoder,
                bank.CharacterTableSha256,
                bank.Complete,
                bank.Rows
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static byte[] Bank(byte[] text)
    {
        byte[] result = new byte[4 + text.Length];
        BinaryPrimitives.WriteInt32LittleEndian(result, 4);
        text.CopyTo(result, 4);
        return result;
    }
}
