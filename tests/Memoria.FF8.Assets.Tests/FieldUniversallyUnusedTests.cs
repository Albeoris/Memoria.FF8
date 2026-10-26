using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldUniversallyUnusedTests
{
    private static readonly string[] Languages = ["en", "fr", "de", "it", "es", "ja"];

    [Fact]
    public void ReferenceInThirdLanguagePreventsUniversalExclusion()
    {
        FieldCompatibilityInventory[] inventories = Languages.Select(language => Inventory(language, 4, language == "de" ? [0, 2] : [0])).ToArray();
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(inventories, Languages);
        Assert.True(evidence.Complete);
        Assert.Equal([1, 3], evidence.Ordinals);
        Assert.Equal(Languages, evidence.Languages);
        Assert.Null(evidence.Uncertainty);
    }

    [Fact]
    public void OrdinalAbsentFromShorterBanksCanStillBeUniversallyUnused()
    {
        FieldCompatibilityInventory[] inventories = Languages.Select(language => Inventory(language, language == "ja" ? 5 : 2, [0])).ToArray();
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(inventories, Languages);
        Assert.True(evidence.Complete);
        Assert.Equal([1, 2, 3, 4], evidence.Ordinals);
    }

    [Fact]
    public void KnownTextIdWithDynamicPresentationRetainsUniversalProof()
    {
        FieldCompatibilityInventory[] inventories = Inventories();
        FieldCompatibilityContext context = inventories[0].Contexts.Single(context => context.IsMessage);
        inventories[0] = inventories[0] with { Contexts = [context with { Dynamic = true }], Problems = ["Presentation unresolved."] };
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(inventories, Languages);
        Assert.True(evidence.Complete);
        Assert.Equal([1, 2], evidence.Ordinals);
    }

    [Fact]
    public void MissingLanguageDoesNotProduceExclusions()
    {
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(Inventories()[..5], Languages);
        Assert.False(evidence.Complete);
        Assert.Empty(evidence.Ordinals);
        Assert.Contains("ja", evidence.Uncertainty);
    }

    [Fact]
    public void UnknownTextIdPreventsExclusionEvenIfValidationFlagIsInconsistent()
    {
        FieldCompatibilityInventory[] inventories = Inventories();
        FieldCompatibilityContext context = inventories[2].Contexts.Single(context => context.IsMessage);
        inventories[2] = inventories[2] with { Contexts = [context with { MessageId = null, Dynamic = true }] };
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(inventories, Languages);
        Assert.False(evidence.Complete);
        Assert.Empty(evidence.Ordinals);
    }

    [Theory]
    [InlineData("script")]
    [InlineData("bank")]
    [InlineData("references")]
    public void InvalidInventoryPreventsExclusion(string invalid)
    {
        FieldCompatibilityInventory[] inventories = Inventories();
        inventories[1] = invalid switch
        {
            "script" => FieldCompatibilityAudit.Inspect(new("fixture", "fr", "synthetic", [1, 2, 3], Bank(3))),
            "bank" => FieldCompatibilityAudit.Inspect(new("fixture", "fr", "synthetic", Script([0]), [new(1, "Invalid ordinal")])),
            _ => FieldCompatibilityAudit.Inspect(new("fixture", "fr", "synthetic", Script([99]), Bank(3)))
        };
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(inventories, Languages);
        Assert.False(evidence.Complete);
        Assert.Empty(evidence.Ordinals);
        Assert.NotNull(evidence.Uncertainty);
    }

    [Fact]
    public void ContradictoryInventoryAndPartialLanguageArgumentsAreRejected()
    {
        FieldCompatibilityInventory[] inventories = Inventories();
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Find(inventories.Append(inventories[0]).ToArray(), Languages));
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Find([inventories[0], inventories[1] with { Field = "other" }], Languages));
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Find(inventories, ["en", "fr"]));
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Find(inventories, ["en", "fr", "de", "it", "es", "EN"]));
    }

    [Fact]
    public void ApplyFiltersOnlyUnusedOrdinalsAndPreservesContextAndNativeDiagnostics()
    {
        FieldCompatibilityInventory[] inventories = Inventories();
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(inventories, Languages);
        FieldCompatibilityResult original = FieldCompatibilityAudit.Compare(Input("en", 3, [0]), Input("fr", 3, [0])) with
        {
            Kind = FieldCompatibilityKind.Incomplete,
            PotentialLocalOrdinals = [0, 1, 2],
            ImplicatedEnglishOrdinals = [0, 1, 2],
            ReviewCases = [Review("alignment", "UnmatchedBankRun", [1, 2]), Review("partial", "UnmatchedExecutableSuffix", [0, 1]), Review("dynamic", "DynamicContext", [1]), Review("choice", "ChoiceGroup", [2])],
            SemanticReviewCases = 4,
            ContextOnlyProblems = 2
        };
        FieldCompatibilityResult result = FieldUniversallyUnused.Apply(original, evidence);
        Assert.Equal([0], result.PotentialLocalOrdinals);
        Assert.Equal([0], result.ImplicatedEnglishOrdinals);
        Assert.Equal(["partial", "dynamic", "choice"], result.ReviewCases.Select(review => review.Id));
        Assert.Equal([0], result.ReviewCases[0].LocalOrdinals);
        Assert.Empty(result.ReviewCases[1].LocalOrdinals);
        Assert.Equal(3, result.SemanticReviewCases);
        Assert.Equal(original.ContextOnlyProblems, result.ContextOnlyProblems);
        Assert.Equal(original.Kind, result.Kind);
        Assert.Equal(original.ProvenOrdinalMatches, result.ProvenOrdinalMatches);
        Assert.Equal(original.Reasons, result.Reasons);
        Assert.Same(original.English, result.English);
        Assert.Same(original.Localized, result.Localized);
        Assert.Equal(original.MessageIdentityEvidence, result.MessageIdentityEvidence);
        Assert.Equal(original.EnglishSuffixContexts, result.EnglishSuffixContexts);
        Assert.Equal(original.FirstDivergence, result.FirstDivergence);
        Assert.Same(evidence, result.UniversallyUnusedEvidence);
    }

    [Fact]
    public void IncompleteEvidenceKeepsCandidatesAndMismatchedEvidenceIsRejected()
    {
        FieldCompatibilityResult original = FieldCompatibilityAudit.Compare(Input("en", 3, [0]), Input("fr", 4, [0]));
        FieldUniversallyUnusedEvidence incomplete = FieldUniversallyUnused.Find(Inventories()[..5], Languages);
        Assert.Equal(original.PotentialLocalOrdinals, FieldUniversallyUnused.Apply(original, incomplete).PotentialLocalOrdinals);
        FieldUniversallyUnusedEvidence complete = FieldUniversallyUnused.Find(Inventories(), Languages);
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Apply(original, complete with { Field = "other" }));
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Apply(original, complete with { Languages = ["en", "fr"] }));
    }

    [Fact]
    public void ApplyRejectsProofThatExcludesAnActuallyReferencedId()
    {
        FieldCompatibilityResult original = FieldCompatibilityAudit.Compare(Input("en", 3, [0]), Input("fr", 3, [0, 1]));
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(Inventories(), Languages);
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Apply(original, evidence));
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Apply(original, evidence with { Ordinals = [0] }));
    }

    [Theory]
    [InlineData("script")]
    [InlineData("bank")]
    [InlineData("references")]
    [InlineData("unknown")]
    [InlineData("range")]
    public void ApplyRejectsProofAgainstInvalidOrUnknownPairState(string invalid)
    {
        FieldCompatibilityResult original = FieldCompatibilityAudit.Compare(Input("en", 3, [0]), Input("fr", 3, [0]));
        FieldCompatibilityInventory local = original.Localized;
        local = invalid switch
        {
            "script" => local with { ScriptValidated = false },
            "bank" => local with { BankValidated = false },
            "references" => local with { StaticReferencesValidated = false },
            "unknown" => local with { Contexts = local.Contexts.Select(context => context.IsMessage ? context with { MessageId = null } : context).ToImmutableArray() },
            _ => local with { ReferencedOrdinals = [99] }
        };
        FieldUniversallyUnusedEvidence evidence = FieldUniversallyUnused.Find(Inventories(), Languages);
        Assert.Throws<ArgumentException>(() => FieldUniversallyUnused.Apply(original with { Localized = local }, evidence));
    }

    private static FieldCompatibilityReviewCase Review(string id, string kind, ImmutableArray<int> ordinals) => new(id, kind, [], [], ordinals, ordinals);
    private static FieldCompatibilityInventory[] Inventories() => Languages.Select(language => Inventory(language, 3, [0])).ToArray();
    private static FieldCompatibilityInventory Inventory(string language, int count, int[] references) => FieldCompatibilityAudit.Inspect(Input(language, count, references));
    private static FieldCompatibilityInput Input(string language, int count, int[] references) => new("fixture", language, "synthetic", Script(references), Bank(count));
    private static FieldCompatibilityText[] Bank(int count) => Enumerable.Range(0, count).Select(ordinal => new FieldCompatibilityText(ordinal, "Synthetic " + ordinal)).ToArray();

    private static byte[] Script(int[] references)
    {
        int[] operations = new[] { Encode(Jsm.Opcode.LBL, 0) }.Concat(references.SelectMany(id => new[] { Encode(Jsm.Opcode.PSHN_L, 0), Encode(Jsm.Opcode.PSHN_L, id), (int)Jsm.Opcode.MES })).Append((int)Jsm.Opcode.RET).ToArray();
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
