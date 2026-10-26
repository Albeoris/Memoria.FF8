using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldLocalizationNumberingTests
{
    [Fact]
    public void SourceOrdinalsHaveFourMinimumDigitsWithoutTruncation()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(0),
            Input(6),
            Input(10000),
            Input(int.MaxValue)
        ]);

        Assert.Equal(["$en_bgroad_1_0000", "$en_bgroad_1_0006", "$en_bgroad_1_10000", "$en_bgroad_1_2147483647"], entries.Select(entry => entry.NewKey));
        Assert.Equal([0, 6, 10000, int.MaxValue], entries.Select(entry => entry.SourceOrdinal));
    }

    [Fact]
    public void ShiftedLanguagesRetainIndependentOriginalPositions()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(11),
            Input(12),
            Input(11, "fr"),
            Input(12, "fr"),
            Input(20, "fr"),
            Input(18, "de")
        ]);

        Assert.Equal(["$en_bgroad_1_0011", "$en_bgroad_1_0012", "$fr_bgroad_1_0011", "$fr_bgroad_1_0012", "$fr_bgroad_1_0020", "$de_bgroad_1_0018"], entries.Select(entry => entry.NewKey));
    }

    [Fact]
    public void MoreThanNinetyNineRowsUseTheirNaturalOrdinals()
    {
        FieldLocalizationNumberingInput[] inputs = Enumerable.Range(0, 125).Select(ordinal => Input(ordinal, "fr")).ToArray();
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.Equal(125, entries.Count);
        Assert.Equal("$fr_bgroad_1_0099", entries[99].NewKey);
        Assert.Equal("$fr_bgroad_1_0100", entries[100].NewKey);
        Assert.Equal("$fr_bgroad_1_0124", entries[^1].NewKey);
        Assert.Equal(entries.Count, entries.Select(entry => entry.NewKey).Distinct().Count());
    }

    [Fact]
    public void PermutationsPreserveSourceIdentityAndFollowInputOrder()
    {
        FieldLocalizationNumberingInput[] inputs = [Input(20, "fr"), Input(2), Input(3, "ja"), Input(0, "de", "other")];
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);
        IReadOnlyList<FieldLocalizationNumberingEntry> reversed = FieldLocalizationNumbering.Allocate(inputs.Reverse().ToArray());

        Assert.Equal(inputs.Select(input => input.Key), entries.Select(entry => entry.OldKey));
        Assert.Equal(entries.Reverse(), reversed);
        Assert.All(entries.Zip(inputs), pair =>
        {
            Assert.Equal(pair.Second.Field, pair.First.Field);
            Assert.Equal(pair.Second.Ordinal, pair.First.SourceOrdinal);
        });
    }

    [Fact]
    public void JapaneseAliasesAndLanguageCaseAreCanonicalized()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([Input(0, "JA"), Input(1, "jp"), Input(2, "EN")]);

        Assert.Equal(["jp", "jp", "en"], entries.Select(entry => entry.Language));
        Assert.Equal(["$jp_bgroad_1_0000", "$jp_bgroad_1_0001", "$en_bgroad_1_0002"], entries.Select(entry => entry.NewKey));
    }

    [Theory]
    [InlineData("ja", "jp")]
    [InlineData("EN", "en")]
    public void DuplicateCanonicalSourcePositionsAreRejected(string firstLanguage, string secondLanguage)
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([
            new("first", firstLanguage, "bgroad_1", 0),
            new("second", secondLanguage, "bgroad_1", 0)
        ]));
    }

    [Fact]
    public void SameOrdinalInDifferentLanguagesAndFieldsIsAllowed()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([Input(0), Input(0, "fr"), Input(0, field: "other")]);

        Assert.Equal(3, entries.Select(entry => entry.NewKey).Distinct().Count());
    }

    [Fact]
    public void DuplicateOldKeysAreRejectedEvenForDifferentSources()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0), Input(1) with { Key = Input(0).Key }]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void MissingOldKeysAreRejected(string? key)
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0) with { Key = key! }]));
    }

    [Fact]
    public void NullCollectionsAndEntriesAreRejectedAndEmptyInputIsAllowed()
    {
        Assert.Throws<ArgumentNullException>(() => FieldLocalizationNumbering.Allocate(null!));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([null!]));
        Assert.Empty(FieldLocalizationNumbering.Allocate([]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ru")]
    public void InvalidLanguagesAreRejected(string? language)
    {
        Assert.ThrowsAny<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0) with { Language = language! }]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("with space")]
    [InlineData("nonascii_é")]
    public void InvalidFieldsAreRejected(string? field)
    {
        Assert.ThrowsAny<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0) with { Field = field! }]));
    }

    [Fact]
    public void NegativeOrdinalsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldLocalizationNumbering.Allocate([Input(-1)]));
    }

    private static FieldLocalizationNumberingInput Input(int ordinal, string language = "en", string field = "bgroad_1")
    {
        return new FieldLocalizationNumberingInput(language + "_" + field + "_" + ordinal, language, field, ordinal);
    }
}
