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
    public void ExplicitMatchesUseTheEnglishBaseAfterAnInsertionRun()
    {
        List<FieldLocalizationNumberingInput> inputs = [Input(11), Input(12), Input(11, language: "fr", englishOrdinals: [11])];
        inputs.AddRange(Enumerable.Range(12, 8).Select(ordinal => Input(ordinal, true, "fr")));
        inputs.Add(Input(20, language: "fr", englishOrdinals: [12]));

        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.Equal("$en_room_001200", entries[1].NewKey);
        Assert.Equal("$fr_room_001200", entries[^1].NewKey);
        Assert.Equal(20, entries[^1].SourceOrdinal);
        Assert.Equal(12, entries[^1].AnchorOrdinal);
        Assert.Equal(Enumerable.Range(1, 8).Select(insertion => "$fr_room_0011" + insertion.ToString("D2")), entries.Skip(3).Take(8).Select(entry => entry.NewKey));
        Assert.All(entries.Skip(3).Take(8), entry => Assert.Equal(11, entry.AnchorOrdinal));
    }

    [Fact]
    public void UnrelatedLanguageInsertionsReserveDifferentGlobalSuffixes()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(6),
            Input(6, language: "fr", englishOrdinals: [6]),
            Input(6, language: "de", englishOrdinals: [6]),
            Input(7, true, "fr"),
            Input(7, true, "de"),
            Input(8, true, "fr")
        ]);

        Assert.Equal([0, 0, 0, 1, 2, 3], entries.Select(entry => entry.Insertion));
        Assert.All(entries, entry => Assert.Equal(6, entry.AnchorOrdinal));
        Assert.Equal(["$fr_room_000601", "$de_room_000602", "$fr_room_000603"], entries.Skip(3).Select(entry => entry.NewKey));
    }

    [Fact]
    public void ExplicitInsertionIdentitySharesOneSlotAcrossFiveLanguages()
    {
        string[] languages = ["fr", "de", "es", "it", "ja"];
        List<FieldLocalizationNumberingInput> inputs = [Input(6)];
        inputs.AddRange(languages.Select(language => Input(6, language: language, englishOrdinals: [6])));
        inputs.AddRange(languages.Select((language, index) => Input(7 + index, true, language) with { InsertionGroup = "shared-resource" }));
        inputs.Add(Input(12, true, "fr"));

        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.All(entries.Skip(6).Take(5), entry =>
        {
            Assert.Equal(6, entry.AnchorOrdinal);
            Assert.Equal(1, entry.Insertion);
        });
        Assert.Equal(["$fr_room_000601", "$de_room_000601", "$es_room_000601", "$it_room_000601", "$jp_room_000601"], entries.Skip(6).Take(5).Select(entry => entry.NewKey));
        Assert.Equal(Enumerable.Range(7, 5), entries.Skip(6).Take(5).Select(entry => entry.SourceOrdinal));
        Assert.Equal(2, entries[^1].Insertion);
        Assert.Equal(entries.Count, entries.Select(entry => entry.NewKey).Distinct().Count());
    }

    [Fact]
    public void DistinctGroupsAndUngroupedExtrasHaveDeterministicSeparateSlots()
    {
        FieldLocalizationNumberingInput[] inputs = [
            Input(6),
            Input(6, language: "fr", englishOrdinals: [6]),
            Input(6, language: "de", englishOrdinals: [6]),
            Input(8, true, "fr") with { InsertionGroup = "first" },
            Input(7, true, "de") with { InsertionGroup = "first" },
            Input(7, true, "fr") with { InsertionGroup = "second" },
            Input(9, true, "de") with { InsertionGroup = "second" },
            Input(10, true, "fr"),
            Input(10, true, "de")
        ];

        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);
        IReadOnlyList<FieldLocalizationNumberingEntry> reversed = FieldLocalizationNumbering.Allocate(inputs.Reverse().ToArray());

        Assert.Equal([0, 0, 0, 2, 2, 1, 1, 3, 4], entries.Select(entry => entry.Insertion));
        Assert.Equal(entries.OrderBy(entry => entry.OldKey), reversed.OrderBy(entry => entry.OldKey));
    }

    [Fact]
    public void InsertionGroupsRespectMergedCommonComponents()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(6),
            Input(8),
            Input(6, language: "fr", englishOrdinals: [6, 8]),
            Input(8, language: "de", englishOrdinals: [8]),
            Input(7, true, "fr") with { InsertionGroup = "shared-resource" },
            Input(9, true, "de") with { InsertionGroup = "shared-resource" }
        ]);

        Assert.All(entries, entry => Assert.Equal(6, entry.AnchorOrdinal));
        Assert.Equal([0, 1, 0, 1, 2, 2], entries.Select(entry => entry.Insertion));
    }

    [Fact]
    public void InsertionGroupsCannotJoinDifferentAnchorsOrFields()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([
            Input(6),
            Input(8),
            Input(6, language: "fr", englishOrdinals: [6]),
            Input(8, language: "de", englishOrdinals: [8]),
            Input(7, true, "fr") with { InsertionGroup = "shared-resource" },
            Input(9, true, "de") with { InsertionGroup = "shared-resource" }
        ]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([
            Input(0, true, "fr") with { InsertionGroup = "shared-resource" },
            Input(0, true, "de", "other") with { InsertionGroup = "shared-resource" }
        ]));
    }

    [Fact]
    public void InsertionGroupsRejectDuplicateCanonicalLanguagesAndOrdinaryKeys()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([
            Input(0, true, "ja") with { InsertionGroup = "shared-resource" },
            Input(1, true, "jp") with { InsertionGroup = "shared-resource" }
        ]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([
            Input(0),
            Input(0, language: "fr", englishOrdinals: [0]) with { InsertionGroup = "shared-resource" }
        ]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyInsertionGroupNamesAreRejected(string identity)
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0, true, "fr") with { InsertionGroup = identity }]));
    }

    [Fact]
    public void DifferentLocalizedWordingCannotSplitAnEstablishedCallsite()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(68),
            Input(68, language: "fr", englishOrdinals: [68]),
            Input(68, language: "de", englishOrdinals: [68]),
            Input(70),
            Input(70, language: "fr", englishOrdinals: [70]),
            Input(70, language: "de", englishOrdinals: [70])
        ]);

        Assert.Equal(["$en_room_006800", "$fr_room_006800", "$de_room_006800", "$en_room_007000", "$fr_room_007000", "$de_room_007000"], entries.Select(entry => entry.NewKey));
        Assert.Equal([68, 68, 68, 70, 70, 70], entries.Select(entry => entry.SourceOrdinal));
    }

    [Fact]
    public void ProvisionalEarlierOrdinalCannotDisplaceAnEstablishedCorrespondence()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(1),
            Input(1, language: "fr"),
            Input(2, language: "fr", englishOrdinals: [1])
        ]);

        Assert.Equal("$en_room_000100", entries[0].NewKey);
        Assert.Equal("$fr_room_000100", entries[2].NewKey);
        Assert.Equal("$fr_room_000101", entries[1].NewKey);
    }

    [Fact]
    public void ManyToManyReferencesMergeAllEnglishBasesAndReserveEnglishTokensFirst()
    {
        FieldLocalizationNumberingInput[] inputs = [
            Input(10),
            Input(12),
            Input(15),
            Input(20, language: "fr", englishOrdinals: [12, 10]),
            Input(22, language: "de", englishOrdinals: [15, 12]),
            Input(30, language: "es", englishOrdinals: [15]),
            Input(21, true, "fr"),
            Input(15, language: "it")
        ];
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.All(entries, entry => Assert.Equal(10, entry.AnchorOrdinal));
        Assert.Equal([0, 1, 2, 0, 1, 2, 3, 2], entries.Select(entry => entry.Insertion));
        Assert.Equal(["$en_room_001000", "$en_room_001001", "$en_room_001002"], entries.Take(3).Select(entry => entry.NewKey));
        AssertReferenceBases(inputs, entries);
    }

    [Fact]
    public void SameLanguageAliasesAndOtherExtraKeysReserveUniqueGlobalSuffixes()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(10),
            Input(20, language: "fr", englishOrdinals: [10]),
            Input(20, language: "de", englishOrdinals: [10]),
            Input(21, language: "fr", englishOrdinals: [10]),
            Input(21, language: "de", englishOrdinals: [10]),
            Input(22, true, "fr"),
            Input(22, language: "de", englishOrdinals: [10])
        ]);

        Assert.Equal([0, 0, 0, 1, 2, 3, 4], entries.Select(entry => entry.Insertion));
        Assert.Equal(entries.Count, entries.Select(entry => entry.NewKey).Distinct().Count());
    }

    [Fact]
    public void ProvisionalOrdinalsShareEnglishOrVirtualBasesWithoutConsumingExtraSlots()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(9),
            Input(9, language: "fr"),
            Input(9, language: "de", englishOrdinals: []),
            Input(14, language: "fr"),
            Input(14, language: "de"),
            Input(15, true, "fr"),
            Input(15, true, "de")
        ]);

        Assert.Equal([9, 9, 9, 14, 14, 14, 14], entries.Select(entry => entry.AnchorOrdinal));
        Assert.Equal([0, 0, 0, 0, 0, 1, 2], entries.Select(entry => entry.Insertion));
    }

    [Fact]
    public void AllocationReturnsInputOrderAndIsIndependentOfInputAndReferenceOrder()
    {
        FieldLocalizationNumberingInput[] inputs = [
            Input(8, true, "de"),
            Input(12, true, "fr"),
            Input(6),
            Input(7, true, "de"),
            Input(11, language: "fr", englishOrdinals: [9, 6]),
            Input(6, language: "de", englishOrdinals: [6]),
            Input(9),
            Input(5, true, "fr", "other")
        ];
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);
        IReadOnlyList<FieldLocalizationNumberingEntry> reversed = FieldLocalizationNumbering.Allocate(inputs.Reverse().Select(input => input with { EnglishKeys = input.EnglishKeys?.Reverse().ToArray() }).ToArray());

        Assert.Equal(inputs.Select(input => input.Key), entries.Select(entry => entry.OldKey));
        Assert.Equal(entries.OrderBy(entry => entry.OldKey), reversed.OrderBy(entry => entry.OldKey));
        AssertReferenceBases(inputs, entries);
    }

    [Fact]
    public void LeadingInsertionsUseAnchorZeroAndJapaneseAliasesShareOneSequence()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(0, true, "JA"),
            Input(1, true, "jp"),
            Input(2, language: "ja"),
            Input(3, true, "JP")
        ]);

        Assert.Equal(["$jp_room_000001", "$jp_room_000002", "$jp_room_000200", "$jp_room_000201"], entries.Select(entry => entry.NewKey));
        Assert.All(entries, entry => Assert.Equal("jp", entry.Language));
        Assert.Equal([0, 0, 2, 2], entries.Select(entry => entry.AnchorOrdinal));
    }

    [Fact]
    public void LeadingInsertionsShareTheFieldZeroComponentAcrossLanguages()
    {
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate([
            Input(0),
            Input(1, true, "fr"),
            Input(1, true, "de"),
            Input(2, language: "fr", englishOrdinals: [0])
        ]);

        Assert.Equal([0, 0, 0, 0], entries.Select(entry => entry.AnchorOrdinal));
        Assert.Equal([0, 1, 2, 0], entries.Select(entry => entry.Insertion));
    }

    [Fact]
    public void NinetyNineGloballyReservedInsertionsAreSupportedAndTheHundredthIsRejected()
    {
        List<FieldLocalizationNumberingInput> inputs = [Input(0), Input(0, language: "fr", englishOrdinals: [0])];
        inputs.AddRange(Enumerable.Range(1, 99).Select(ordinal => Input(ordinal, true, "fr")));
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.Equal("$fr_room_000099", entries[^1].NewKey);
        Assert.Equal(99, entries[^1].Insertion);
        inputs.Add(Input(1, true, "de"));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate(inputs));
    }

    [Fact]
    public void ReservedEnglishTokensCountTowardsTheSuffixLimit()
    {
        List<FieldLocalizationNumberingInput> inputs = Enumerable.Range(0, 100).Select(ordinal => Input(ordinal)).ToList();
        inputs.Add(Input(100, language: "fr", englishOrdinals: Enumerable.Range(0, 100).ToArray()));
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);

        Assert.Equal("$en_room_000099", entries[99].NewKey);
        Assert.Equal("$fr_room_000000", entries[^1].NewKey);
        inputs.Add(Input(101, true, "fr"));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate(inputs));
    }

    [Fact]
    public void DuplicateKeysAndCanonicalSourceOrdinalsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(0), Input(1) with { Key = "en_room_0" }]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1, language: "ja"), Input(1, language: "jp")]));
    }

    [Fact]
    public void InvalidEnglishReferencesAndConflictingClassificationsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1, language: "fr", englishOrdinals: [1])]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1, language: "de"), Input(1, language: "fr") with { EnglishKeys = ["de_room_1"] }]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1, field: "other"), Input(1, language: "fr") with { EnglishKeys = ["en_other_1"] }]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1), Input(2, language: "fr") with { EnglishKeys = [null!] }]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1, true)]));
        Assert.Throws<ArgumentException>(() => FieldLocalizationNumbering.Allocate([Input(1), Input(2, true, "fr", englishOrdinals: [1])]));
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

    private static void AssertReferenceBases(IReadOnlyList<FieldLocalizationNumberingInput> inputs, IReadOnlyList<FieldLocalizationNumberingEntry> entries)
    {
        Dictionary<string, FieldLocalizationNumberingEntry> byKey = entries.ToDictionary(entry => entry.OldKey);
        foreach (FieldLocalizationNumberingInput input in inputs)
        {
            foreach (string englishKey in input.EnglishKeys ?? [])
            {
                Assert.Equal(byKey[englishKey].AnchorOrdinal, byKey[input.Key].AnchorOrdinal);
            }
        }
    }

    private static FieldLocalizationNumberingInput Input(int ordinal, bool languageOnly = false, string language = "en", string field = "room", int[]? englishOrdinals = null)
    {
        return new FieldLocalizationNumberingInput($"{language}_{field}_{ordinal}", language, field, ordinal, languageOnly, englishOrdinals?.Select(english => $"en_{field}_{english}").ToArray());
    }
}
