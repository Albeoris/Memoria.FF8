using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterPatchTests
{
    private static readonly EncounterPatchBaseline Baseline = new("ff8.disc1.edea", "classic.test", ["original"]);

    [Fact]
    public void IndependentHpAndPolicyEditsComposeInEitherOrder()
    {
        EncounterPatchLayer hp = Layer("hp", Hp("boost", "1", "1.25"));
        EncounterPatchLayer policy = Layer("policy", Policy());
        EncounterPatchComposition first = EncounterPatchComposer.Compose(Baseline, [hp, policy]);
        EncounterPatchComposition second = EncounterPatchComposer.Compose(Baseline, [policy, hp]);
        Assert.Equal(1.25, first.ActorProperties["original/stats.maximumHpMultiplier"]);
        Assert.Equal(first.ActorProperties["original/stats.maximumHpMultiplier"], second.ActorProperties["original/stats.maximumHpMultiplier"]);
        Assert.Equal("Regen", first.Policies["policy/support"].Rules.Single().Action.Spell);
        Assert.Equal("Regen", second.Policies["policy/support"].Rules.Single().Action.Spell);
    }

    [Fact]
    public void ProducerDependencyPrecedesEarlierCatalogConsumerAndEditsNamedRule()
    {
        EncounterPatchLayer producer = Layer("base", Policy("""
            {"id":"other","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"Protect","target":"self"}},
            """));
        EncounterPatchLayer consumer = Layer("refine", """
            {"id":"change","kind":"SetRuleProperty","producer":{"documentId":"base","operationId":"define"},"policy":"support","rule":"regen","property":"action.spell","expectedValue":"Regen","value":"Haste"}
            """);
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [consumer, producer]);
        EncounterPatchPolicy policy = result.Policies["base/support"];
        Assert.Equal("Protect", policy.Rules.Single(rule => rule.Id == "other").Action.Spell);
        Assert.Equal("Haste", policy.Rules.Single(rule => rule.Id == "regen").Action.Spell);
        Assert.Equal("base", result.Outcomes[0].Origin.DocumentId);
    }

    [Fact]
    public void SamePayloadFilenameDoesNotReplaceAnotherDocument()
    {
        EncounterPatchLayer first = Layer("hp", Hp("boost", "1", "1.25"));
        EncounterPatchLayer second = Layer("policy", Policy());
        Assert.Equal(first.PayloadPath, second.PayloadPath);
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [first, second]);
        Assert.Equal(2, result.Outcomes.Length);
    }

    [Fact]
    public void ConflictingWritesRejectWithoutMutatingAnEarlierCompleteResult()
    {
        EncounterPatchLayer first = Layer("first", Hp("boost", "1", "1.25"));
        EncounterPatchComposition accepted = EncounterPatchComposer.Compose(Baseline, [first]);
        EncounterPatchLayer second = Layer("second", Hp("conflict", "1.25", "2"));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [first, second]));
        Assert.Contains("second", error.Message);
        Assert.Contains("conflict", error.Message);
        Assert.Contains("first", error.Message);
        Assert.Contains("atomically", error.Message);
        Assert.Equal(1.25, accepted.ActorProperties["original/stats.maximumHpMultiplier"]);
    }

    [Fact]
    public void GuardedHpOverrideRequiresActualPreviousWriter()
    {
        EncounterPatchLayer first = Layer("first", Hp("boost", "1", "1.25"));
        EncounterPatchLayer second = Layer("second", Hp("refine", "1.25", "2", """, "producer":{"documentId":"first","operationId":"boost"}"""));
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [second, first]);
        Assert.Equal(2, result.ActorProperties["original/stats.maximumHpMultiplier"]);
    }

    [Fact]
    public void RedundantEnsureDoesNotBecomeProducer()
    {
        EncounterPatchLayer layer = Layer("same", Policy() + "," + Policy().Replace("\"define\"", "\"redundant\"") + """
            ,{"id":"refine","kind":"SetRuleProperty","producer":{"documentId":"same","operationId":"redundant"},"policy":"support","rule":"regen","property":"action.spell","expectedValue":"Regen","value":"Haste"}
            """);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [layer]));
        Assert.Contains("did not create", error.Message);
    }

    [Fact]
    public void MissingProducerAndCycleHaveActionableDiagnostics()
    {
        EncounterPatchLayer missing = Layer("missing", Hp("a", "1", "2", """, "producer":{"documentId":"absent","operationId":"a"}"""));
        Assert.Contains("Missing producer", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [missing])).Message);
        EncounterPatchLayer first = Layer("first", Hp("a", "1", "2", """, "producer":{"documentId":"second","operationId":"b"}"""));
        EncounterPatchLayer second = Layer("second", Hp("b", "2", "3", """, "producer":{"documentId":"first","operationId":"a"}"""));
        Assert.Contains("cycle", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [first, second])).Message);
    }

    [Fact]
    public void OverlappingRulesRequireExplicitDistinctPriorities()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Layer("bad", Policy("""
            {"id":"other","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Protect","target":"self"}},
            """)));
        Assert.Contains("overlap", error.Message);
    }

    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"atomic\":true", "\"atomic\":false")]
    [InlineData("\"value\":1.25", "\"value\":101")]
    [InlineData("\"kind\":\"SetActorProperty\"", "\"kind\":\"RunCode\"")]
    [InlineData("\"value\":1.25", "\"value\":1.25,\"script\":\"unsafe\"")]
    [InlineData("\"value\":1.25", "\"value\":1.25,\"value\":2")]
    [InlineData("\"value\":1.25", "\"value\":1.25,\"policy\":\"irrelevant\"")]
    public void InvalidExecutablePayloadIsRejected(string before, string after)
    {
        string json = Document("test", Hp("boost", "1", "1.25")).Replace(before, after, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => EncounterPatchDocument.Parse(json));
    }

    [Fact]
    public void MetadataRemainsDescriptiveAndRoundTrips()
    {
        string json = Document("test", Hp("boost", "1", "1.25")).Replace("\"operations\":", "\"metadata\":{\"arbitrary\":{\"script\":\"description only\"}},\"operations\":", StringComparison.Ordinal);
        EncounterPatchDocument document = EncounterPatchDocument.Parse(json);
        EncounterPatchDocument restored = EncounterPatchDocument.Parse(JsonSerializer.Serialize(document, EncounterPatchDocument.JsonOptions));
        Assert.Equal("description only", restored.Metadata!["arbitrary"].GetProperty("script").GetString());
    }

    [Fact]
    public void SourceProfileAndMissingActorAreRejectedAgainstSelectedBaseline()
    {
        EncounterPatchLayer incompatible = Layer("profile", Hp("boost", "1", "1.25")) with { Document = EncounterPatchDocument.Parse(Document("profile", Hp("boost", "1", "1.25")).Replace("\"atomic\":true", "\"atomic\":true,\"sourceProfile\":\"wrong\"", StringComparison.Ordinal)) };
        Assert.Contains("profile", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [incompatible])).Message);
        EncounterPatchLayer absent = Layer("absent", Hp("boost", "1", "1.25").Replace("\"original\"", "\"unknown\""));
        Assert.Contains("unknown", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [absent])).Message);
    }

    [Fact]
    public void DuplicateDocumentAndOperationIdsAreRejected()
    {
        EncounterPatchLayer layer = Layer("same", Hp("boost", "1", "1.25"));
        Assert.Contains("Duplicate document", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [layer, layer])).Message);
        Assert.Contains("Duplicate operation", Assert.Throws<InvalidDataException>(() => EncounterPatchDocument.Parse(Document("same", Hp("boost", "1", "1.25") + "," + Hp("boost", "1", "1.25")))).Message);
    }

    [Fact]
    public void ForwardLocalRuleReferenceUsesStableId()
    {
        EncounterPatchLayer layer = Layer("local", """
            {"id":"refine","kind":"SetRuleProperty","policy":"support","rule":"regen","property":"action.spell","expectedValue":"Regen","value":"Haste"},
            """ + Policy());
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [layer]);
        Assert.Equal("Haste", result.Policies["local/support"].Rules.Single().Action.Spell);
    }

    [Fact]
    public void RemovingRuleRequiresWholeDefinitionGuard()
    {
        EncounterPatchLayer layer = Layer("local", Policy("""
            {"id":"other","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"Protect","target":"self"}},
            """) + """
            ,{"id":"remove","kind":"RemoveRule","producer":{"documentId":"local","operationId":"define"},"policy":"support","rule":"regen","expectedValue":{"id":"regen","when":{"ownTurn":3},"action":{"kind":"Cast","spell":"Regen","target":"self"}}}
            """);
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [layer]);
        Assert.Equal("other", result.Policies["local/support"].Rules.Single().Id);
    }

    [Fact]
    public void DisjointGuardedSpellWeightEditsComposeByCanonicalSpellIdentity()
    {
        EncounterPatchLayer producer = Layer("base", WeightedPolicy());
        EncounterPatchLayer fire = Layer("fire", WeightEdit("increase", "base", "define", "Fire", 1, 3));
        EncounterPatchLayer blizzard = Layer("ice", WeightEdit("increase", "base", "define", "Blizzard", 1, 2));
        EncounterPatchComposition first = EncounterPatchComposer.Compose(Baseline, [fire, blizzard, producer]);
        EncounterPatchComposition second = EncounterPatchComposer.Compose(Baseline, [blizzard, fire, producer]);
        EncounterPatchAction action = first.Policies["base/policy"].Rules.Single().Action;
        Assert.Equal(3, action.SpellWeights["Fire"]);
        Assert.Equal(2, action.SpellWeights["Blizzard"]);
        Assert.Equal(action.SpellWeights["Fire"], second.Policies["base/policy"].Rules.Single().Action.SpellWeights["Fire"]);
        Assert.Equal(action.SpellWeights["Blizzard"], second.Policies["base/policy"].Rules.Single().Action.SpellWeights["Blizzard"]);
    }

    [Fact]
    public void ChainedWeightRefinementReferencesTheActualWriterWithoutChangingEntityScope()
    {
        EncounterPatchLayer producer = Layer("base", WeightedPolicy());
        EncounterPatchLayer first = Layer("first", WeightEdit("increase", "base", "define", "Fire", 1, 3));
        EncounterPatchLayer second = Layer("second", WeightEdit("refine", "first", "increase", "Fire", 3, 4));
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [second, first, producer]);
        Assert.Equal(4, result.Policies["base/policy"].Rules.Single().Action.SpellWeights["Fire"]);
    }

    [Fact]
    public void ConflictingSpellWeightWriterAndWrongGuardAreRejectedAtomically()
    {
        EncounterPatchLayer producer = Layer("base", WeightedPolicy());
        EncounterPatchLayer first = Layer("first", WeightEdit("increase", "base", "define", "Fire", 1, 3));
        EncounterPatchLayer conflict = Layer("conflict", WeightEdit("increase", "base", "define", "Fire", 3, 4));
        Assert.Contains("Conflicting write", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [producer, first, conflict])).Message);
        EncounterPatchLayer wrongGuard = Layer("wrong", WeightEdit("increase", "base", "define", "Fire", 2, 3));
        Assert.Contains("expected 2, observed 1", Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [producer, wrongGuard])).Message);
    }

    private static string WeightedPolicy() => """
        {"id":"define","kind":"AddActionPolicy","policy":"policy","rules":[{"id":"pool","when":{"ownTurnAtLeast":1},"action":{"kind":"CastFromPool","spells":["Fire","Blizzard"],"target":"self"}}]}
        """;

    [Theory]
    [InlineData("Blizaga", "Blizzaga")]
    [InlineData("Darkness", "Blind")]
    [InlineData("Reflection", "Reflect")]
    public void SpellAliasesNormalizeExecutableReferencesWithoutRewritingMetadata(string alias, string canonical)
    {
        EncounterPatchLayer layer = Layer("aliases", $$$"""
            {"id":"define","kind":"AddActionPolicy","policy":"policy","metadata":{"description":"{{{alias}}}"},"rules":[{"id":"one","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"{{{alias}}}","target":"self"}}]}
            """);
        Assert.Equal(canonical, layer.Document.Operations.Single().Rules.Single().Action.Spell);
        Assert.Equal(alias, layer.Document.Operations.Single().Metadata!["description"].GetString());
    }

    [Fact]
    public void AliasAndCanonicalSpellWeightPathsShareOneConflictOwnerForProgrammaticDocuments()
    {
        EncounterPatchLayer producer = Layer("base", WeightedPolicy().Replace("\"Fire\"", "\"Blizaga\"", StringComparison.Ordinal));
        EncounterPatchLayer first = Layer("first", WeightEdit("increase", "base", "define", "Blizzaga", 1, 2));
        first = first with { Document = first.Document with { Operations = [first.Document.Operations.Single() with { Property = "action.spellWeights.Blizaga" }] } };
        EncounterPatchLayer conflict = Layer("second", WeightEdit("increase", "base", "define", "Blizzaga", 2, 3));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => EncounterPatchComposer.Compose(Baseline, [producer, first, conflict]));
        Assert.Contains("Conflicting write", error.Message);
        Assert.Contains("action.spellWeights.Blizzaga", error.Message);
        Assert.Contains("first", error.Message);
    }

    [Fact]
    public void DuplicateAliasWeightKeysAndDuplicateCanonicalPoolEntriesAreRejected()
    {
        const string weights = """
            {"id":"define","kind":"AddActionPolicy","policy":"policy","rules":[{"id":"pool","when":{"ownTurn":1},"action":{"kind":"CastFromPool","spells":["Blizzaga"],"spellWeights":{"Blizaga":1,"Blizzaga":2},"target":"self"}}]}
            """;
        Assert.Contains("Duplicate spell-weight identity", Assert.Throws<InvalidDataException>(() => Layer("aliases", weights)).Message);
        Assert.Throws<InvalidDataException>(() => Layer("aliases", WeightedPolicy().Replace("\"Fire\"", "\"Blizaga\"", StringComparison.Ordinal).Replace("\"Blizzard\"", "\"Blizzaga\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void AliasedGuardAndReplacementSpellValuesUseTheSameCanonicalRuleIdentity()
    {
        EncounterPatchLayer producer = Layer("base", """
            {"id":"define","kind":"AddActionPolicy","policy":"policy","rules":[{"id":"one","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"Blind","target":"self"}}]}
            """);
        EncounterPatchLayer consumer = Layer("consumer", """
            {"id":"change","kind":"SetRuleProperty","producer":{"documentId":"base","operationId":"define"},"policy":"policy","rule":"one","property":"action.spell","expectedValue":"Darkness","value":"Reflection"}
            """);
        EncounterPatchComposition result = EncounterPatchComposer.Compose(Baseline, [consumer, producer]);
        Assert.Equal("Reflect", result.Policies["base/policy"].Rules.Single().Action.Spell);
    }

    private static string WeightEdit(string id, string document, string operation, string spell, int expected, int value) => $$"""
        {"id":"{{id}}","kind":"SetRuleProperty","producer":{"documentId":"{{document}}","operationId":"{{operation}}"},"policy":"policy","rule":"pool","property":"action.spellWeights.{{spell}}","expectedValue":{{expected}},"value":{{value}}}
        """;
    private static EncounterPatchLayer Layer(string id, string operations) => new(id + "-package", "gameplay/encounters/shared.json", EncounterPatchDocument.Parse(Document(id, operations)));

    private static string Document(string id, string operations) => "{\"schemaVersion\":1,\"documentId\":\"" + id + "\",\"encounter\":\"ff8.disc1.edea\",\"atomic\":true,\"operations\":[" + operations + "]}";

    private static string Hp(string id, string expected, string value, string extra = "") => "{\"id\":\"" + id + "\",\"kind\":\"SetActorProperty\",\"actor\":\"original\",\"property\":\"stats.maximumHpMultiplier\",\"expectedValue\":" + expected + ",\"value\":" + value + extra + "}";

    private static string Policy(string preceding = "") => """
        {"id":"define","kind":"AddActionPolicy","policy":"support","rules":[
        """ + preceding + """
        {"id":"regen","when":{"ownTurn":3},"action":{"kind":"Cast","spell":"Regen","target":"self"}}]}
        """;
}
