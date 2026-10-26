using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldLocalizationNumberingTests
{
    [Fact]
    public void OrdinaryKeysRetainTheirSourceOrdinalWithZeroSuffix()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(0),
            Input(6),
            Input(10000),
            Input(int.MaxValue)
        ]);

        Assert.Equal(["$en_room_000000", "$en_room_000600", "$en_room_1000000", "$en_room_214748364700"], entries.Select(entry => entry.NewKey));
        Assert.All(entries, entry =>
        {
            Assert.Equal(entry.SourceOrdinal, entry.AnchorOrdinal);
            Assert.Equal(0, entry.Insertion);
        });
    }

    [Fact]
    public void InsertionsUseThePrecedingMatchedSourceOrdinalAndResetAtTheNextMatch()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(6),
            Input(7, true),
            Input(8, true),
            Input(9),
            Input(10, true)
        ]);

        Assert.Equal(["$en_room_000600", "$en_room_000601", "$en_room_000602", "$en_room_000900", "$en_room_000901"], entries.Select(entry => entry.NewKey));
        Assert.Equal([6, 7, 8, 9, 10], entries.Select(entry => entry.SourceOrdinal));
        Assert.Equal([6, 6, 6, 9, 9], entries.Select(entry => entry.AnchorOrdinal));
        Assert.Equal([0, 1, 2, 0, 1], entries.Select(entry => entry.Insertion));
        Assert.Equal(5, entries.Select(entry => entry.NewKey).Distinct().Count());
    }

    [Fact]
    public void AllocationSortsWithinEachPairAndReturnsOriginalInputOrder()
    {
        FieldLocalizationNumberingInput[] inputs = [
            Input(8, true),
            Input(12, true, "fr"),
            Input(6),
            Input(7, true),
            Input(11, false, "fr"),
            Input(5, true, field: "other")
        ];
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.Equal(inputs.Select(input => input.Key), entries.Select(entry => entry.OldKey));
        Assert.Equal(["$en_room_000602", "$fr_room_001101", "$en_room_000600", "$en_room_000601", "$fr_room_001100", "$en_other_000001"], entries.Select(entry => entry.NewKey));
        IReadOnlyList<FieldLocalizationNumberingEntry> reversed = FieldLocalizationNumbering.Allocate(inputs.Reverse().ToArray());
        Assert.Equal(entries.OrderBy(entry => entry.OldKey), reversed.OrderBy(entry => entry.OldKey));
    }

    [Fact]
    public void LeadingInsertionsUseAnchorZeroAndJapaneseAliasesShareOneSequence()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(0, true, "JA"),
            Input(1, true, "jp"),
            Input(2, false, "ja"),
            Input(3, true, "JP")
        ]);

        Assert.Equal(["$jp_room_000001", "$jp_room_000002", "$jp_room_000200", "$jp_room_000201"], entries.Select(entry => entry.NewKey));
        Assert.All(entries, entry => Assert.Equal("jp", entry.Language));
        Assert.Equal([0, 0, 2, 2], entries.Select(entry => entry.AnchorOrdinal));
    }

    [Fact]
    public void NinetyNineInsertionsAreSupportedAndTheHundredthIsRejected()
    {
        FieldLocalizationNumberingInput[] inputs = Enumerable.Range(0, 101).Select(ordinal => Input(ordinal, ordinal > 0)).ToArray();
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs.Take(100).ToArray());

        Assert.Equal("$en_room_000099", entries[^1].NewKey);
        Assert.Equal(99, entries[^1].Insertion);
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate(inputs));
    }

    [Fact]
    public void DuplicateKeysAndCanonicalSourceOrdinalsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0), Input(1) with { Key = "en_room_0" }]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1, language: "ja"), Input(1, language: "jp")]));
    }

    [Theory]
    [InlineData("", "en", "room", 0)]
    [InlineData(" ", "en", "room", 0)]
    [InlineData("key", "ru", "room", 0)]
    [InlineData("key", "en", "", 0)]
    [InlineData("key", "en", "unsafe field", 0)]
    [InlineData("key", "en", "room", -1)]
    public void InvalidInputsAreRejected(string key, string language, string field, int ordinal)
    {
        Assert.ThrowsAny<ArgumentException>(() => FieldLocalizationNumbering.Allocate([new(key, language, field, ordinal, false)]));
    }

    [Fact]
    public void NullInputsAndNullRecordsAreRejectedAndEmptyInputIsSupported()
    {
        Assert.Throws<ArgumentNullException>(() => FieldLocalizationNumbering.Allocate(null!));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([null!]));
        Assert.Empty(FieldLocalizationNumbering.Allocate([]));
    }

    private static FieldLocalizationNumberingInput Input(int ordinal, bool languageOnly = false, string language = "en", string field = "room")
    {
        return new FieldLocalizationNumberingInput($"{language}_{field}_{ordinal}", language, field, ordinal, languageOnly);
    }
}
