using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldLocalizationMappingTests
{
    private const string Field = "bgroom_6";
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "ja"];

    [Fact]
    public void KeysAreLanguageUniqueAndJapaneseAliasesUseJp()
    {
        Assert.Equal("$en_bgroom_6_000000", FieldLocalizationKey.Create("en", Field, 0));
        Assert.Equal("$jp_bgroom_6_000600", FieldLocalizationKey.Create("ja", Field, 6));
        Assert.Equal(FieldLocalizationKey.Create("ja", Field, 6), FieldLocalizationKey.Create("jp", Field, 6));
        Assert.Equal("$en_bgroom_6_1000000", FieldLocalizationKey.Create("en", Field, 10000));
        Assert.Equal("$en_sample-room_1_000600", FieldLocalizationKey.Create("en", "sample-room_1", 6));
        Assert.Equal(6, Languages.Select(language => FieldLocalizationKey.Create(language, Field, 0)).Distinct().Count());
        Assert.NotEqual(FieldLocalizationKey.Create("en", "bgroom_6", 1), FieldLocalizationKey.Create("en", "bgroom_7", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("with space")]
    [InlineData("quoted\"field")]
    [InlineData("nonascii_é")]
    public void UnsafeFieldIdentifiersAreRejected(string field)
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationKey.Create("en", field, 0));
    }

    [Fact]
    public void InvalidLanguageAndNegativeOrdinalAreRejected()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationKey.Create("ru", Field, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldLocalizationKey.Create("en", Field, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldLocalizationKey.Create("en", Field, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldLocalizationKey.Create("en", Field, 1, 100));
        Assert.Equal("$jp_bgroom_6_000101", FieldLocalizationKey.Create("jp", Field, 1, 1));
    }

    [Fact]
    public void MappingIncludesEveryBankSlotIncludingUnusedAndLocalizedExtras()
    {
        FieldCompatibilityInventory[] inventories = Languages.Select(language => Inventory(language, language == "ja" ? 5 : 3)).ToArray();
        FieldUniversallyUnusedEvidence unused = FieldUniversallyUnused.Find(inventories, Languages);
        using StringWriter writer = new();
        int count = FieldLocalizationMapping.WriteCsv(writer, Field, inventories, [], unused);
        string[][] rows = Rows(writer);
        Assert.Equal(5, count);
        Assert.Equal("en,fr,de,es,it,jp,Field,Ordinal,Field Name,fr Status,de Status,es Status,it Status,jp Status,Unused Across Languages,en Source Ordinal,fr Source Ordinal,de Source Ordinal,es Source Ordinal,it Source Ordinal,jp Source Ordinal", string.Join(',', rows[0]));
        Assert.Equal(6, rows.Length);
        Assert.Equal("$en_bgroom_6_000000", rows[1][0]);
        Assert.Equal("$jp_bgroom_6_000400", rows[5][5]);
        Assert.Empty(rows[5][0]);
        Assert.Equal("4", rows[5][7]);
        Assert.Equal("UnusedAllLanguages", rows[5][13]);
        Assert.Equal("true", rows[5][14]);
        Assert.Equal("false", rows[1][14]);
        Assert.Equal("0", rows[1][15]);
        Assert.Empty(rows[5][15]);
        Assert.Equal("4", rows[5][20]);
        Assert.DoesNotContain("Synthetic dialogue", writer.ToString());
    }

    [Fact]
    public void StatusRequiresBothActualProofAndAbsenceFromEitherReviewBound()
    {
        FieldCompatibilityResult pair = Pair("fr", 3) with
        {
            PotentialLocalOrdinals = [1],
            ImplicatedEnglishOrdinals = [2]
        };
        using StringWriter writer = new();
        FieldLocalizationMapping.WriteCsv(writer, Field, [pair.English, pair.Localized], [pair]);
        string[][] rows = Rows(writer);
        Assert.Equal("Proven", rows[1][9]);
        Assert.Equal("RequiresReview", rows[2][9]);
        Assert.Equal("RequiresReview", rows[3][9]);
        FieldCompatibilityResult unproven = pair with { ProvenOrdinalMatches = [], PotentialLocalOrdinals = [], ImplicatedEnglishOrdinals = [] };
        using StringWriter unprovenWriter = new();
        FieldLocalizationMapping.WriteCsv(unprovenWriter, Field, [unproven.English, unproven.Localized], [unproven]);
        Assert.Equal("Unproven", Rows(unprovenWriter)[1][9]);
    }

    [Fact]
    public void MissingUnknownTechnicalAndAbsentSlotsHaveExplicitStatuses()
    {
        FieldCompatibilityInventory english = Inventory("en", 2);
        FieldCompatibilityInventory unknown = Inventory("fr", 2) with { BankCountKnown = false, DeclaredTextCount = null, BankValidated = false };
        FieldCompatibilityInventory extra = Inventory("de", 4);
        FieldCompatibilityInventory spanish = Inventory("es", 2);
        FieldCompatibilityInventory broken = Inventory("it", 2) with { ScriptValidated = false };
        using StringWriter writer = new();
        FieldLocalizationMapping.WriteCsv(writer, Field, [english, unknown, extra, spanish, broken], []);
        string[][] rows = Rows(writer);
        Assert.Empty(rows[1][1]);
        Assert.Equal("UnknownBank", rows[1][9]);
        Assert.Equal("NotCompared", rows[1][10]);
        Assert.Equal("NotCompared", rows[1][11]);
        Assert.Equal("TechnicalUncertain", rows[1][12]);
        Assert.Equal("AbsentLocalizedSlot", rows[1][13]);
        Assert.Equal("NoEnglishSlot", rows[3][10]);
        Assert.Equal("AbsentLocalizedSlot", rows[3][11]);
        Assert.Equal("Unknown", rows[1][14]);
    }

    [Fact]
    public void MissingEnglishDoesNotInventEnglishKeys()
    {
        using StringWriter writer = new();
        FieldLocalizationMapping.WriteCsv(writer, Field, [Inventory("fr", 1)], []);
        string[][] rows = Rows(writer);
        Assert.Empty(rows[1][0]);
        Assert.Equal("NoEnglishSlot", rows[1][9]);
    }

    [Fact]
    public void PlaceholderRetainsFieldsWithUnknownOrEmptyBanksWithoutInventingKeys()
    {
        FieldCompatibilityInventory unknown = Inventory("fr", 1) with { BankCountKnown = false, DeclaredTextCount = null, BankValidated = false };
        using StringWriter writer = new();
        int count = FieldLocalizationMapping.WriteCsv(writer, Field, [Inventory("en", 0), unknown, Inventory("de", 0)], []);
        string[][] rows = Rows(writer);
        Assert.Equal(1, count);
        Assert.Equal(2, rows.Length);
        Assert.All(rows[1].Take(6), Assert.Empty);
        Assert.Equal(Field, rows[1][6]);
        Assert.Empty(rows[1][7]);
        Assert.Equal("UnknownBank", rows[1][9]);
        Assert.Equal("EmptyBank", rows[1][10]);
        Assert.Equal("Unknown", rows[1][14]);
    }

    [Fact]
    public void CsvEscapesDisplayNameAndCanAppendWithoutHeader()
    {
        const string name = "Room, \"Lobby\"\r\nNext line";
        using StringWriter writer = new();
        FieldLocalizationMapping.WriteCsv(writer, Field, [Inventory("en", 1)], [], includeHeader: false, fieldName: name);
        Assert.StartsWith("$en_bgroom_6_000000,", writer.ToString());
        Assert.Contains(",\"Room, \"\"Lobby\"\"\r\nNext line\",", writer.ToString());
        Assert.DoesNotContain("fr Status", writer.ToString());
    }

    [Fact]
    public void ContradictoryInventoriesComparisonsAndUnusedProofAreRejected()
    {
        FieldCompatibilityResult pair = Pair("fr", 2);
        using StringWriter writer = new();
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, [Inventory("ja", 1), Inventory("jp", 1)], []));
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, [pair.English with { Field = "other" }], []));
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, [pair.English, pair.Localized], [pair, pair]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, [pair.English, pair.Localized with { JsmSha256 = "different" }], [pair]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, [pair.English, pair.Localized with { Bank = default }], [pair]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, [pair.English, pair.Localized with { ReferencedOrdinals = default }], [pair]));
        FieldCompatibilityInventory[] inventories = Languages.Select(language => Inventory(language, 2)).ToArray();
        FieldUniversallyUnusedEvidence unused = FieldUniversallyUnused.Find(inventories, Languages);
        Assert.Throws<ArgumentException>(() => FieldLocalizationMapping.WriteCsv(writer, Field, inventories, [], unused with { Ordinals = [0] }));
    }

    private static string[][] Rows(StringWriter writer) => writer.ToString().Split(writer.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(',')).ToArray();
    private static FieldCompatibilityInventory Inventory(string language, int count) => FieldCompatibilityAudit.Inspect(Input(language, count));
    private static FieldCompatibilityResult Pair(string language, int count) => FieldCompatibilityAudit.Compare(Input("en", count), Input(language, count));
    private static FieldCompatibilityInput Input(string language, int count) => new(Field, language, "synthetic", Script(count == 0), Enumerable.Range(0, count).Select(ordinal => new FieldCompatibilityText(ordinal, "Synthetic dialogue " + ordinal)).ToArray());

    private static byte[] Script(bool empty)
    {
        int[] operations = empty ? [Encode(Jsm.Opcode.LBL, 0), (int)Jsm.Opcode.RET] : [Encode(Jsm.Opcode.LBL, 0), Encode(Jsm.Opcode.PSHN_L, 0), Encode(Jsm.Opcode.PSHN_L, 0), (int)Jsm.Opcode.MES, (int)Jsm.Opcode.RET];
        byte[] data = new byte[14 + operations.Length * 4];
        data[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(14 + index * 4), operations[index]);
        }
        return data;
    }

    private static int Encode(Jsm.Opcode opcode, int value) => ((int)opcode << 24) | (value & 0xFFFFFF);
}
