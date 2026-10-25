using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PlanningTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "memoria-planning-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SealedPlanDetectsTamperingAndNeverOverwrites()
    {
        (EvidenceStore store, CampaignPlanning planning) = Create();
        Assert.True(planning.Validate().Valid);
        planning.Seal();
        Assert.Throws<IOException>(() => planning.Seal());
        Assert.Throws<IOException>(() => store.WriteNew("inventory.json", store.Read<InventoryRecord>("inventory.json")));
        Assert.True(planning.Validate().Valid);
    }

    [Fact]
    public void FinalPassCannotOmitCasesAndHasIndependentClaims()
    {
        (EvidenceStore store, CampaignPlanning planning) = Create();
        planning.Seal();
        BuildRecord build = store.Read<BuildRecord>("build.json");
        Assert.Throws<InvalidDataException>(() => planning.CreatePass("incomplete", "final", build, []));
        PassRecord baseline = planning.CreatePass("baseline", "baseline", build, null);
        PassRecord final = planning.CreatePass("final", "final", build, null);
        Assert.Equal(baseline.RequiredCases, final.RequiredCases);
        LocalCoordinator coordinator = new(store.Root);
        CoordinationScope first = new(baseline.CampaignId, baseline.PlanHash, baseline.RunPassId);
        CoordinationScope last = new(final.CampaignId, final.PlanHash, final.RunPassId);
        coordinator.RegisterEffects(first, ["synthetic-effect"]);
        coordinator.RegisterEffects(last, ["synthetic-effect"]);
        Assert.NotNull(coordinator.ClaimNext(first, "worker", ProcessIdentity.Current));
        Assert.NotNull(coordinator.ClaimNext(last, "worker", ProcessIdentity.Current));
    }

    [Fact]
    public void UnresolvedAndUnmappedCoverageCannotSeal()
    {
        (_, CampaignPlanning planning) = Create(entryTransform: entry => entry with { Disposition = "unresolved", CaseIds = [], Reason = "Owner has not been established." });
        Assert.False(planning.Validate().Valid);
        Assert.Throws<InvalidDataException>(() => planning.Seal());
    }

    [Fact]
    public void WrongBindingAndIllegalPlayerOwnerArePlanningErrors()
    {
        (_, CampaignPlanning planning) = Create(caseTransform: recipe => recipe with { RequestedEffectId = 2, Scenario = recipe.Scenario with { Caster = "other" } });
        PlanValidation validation = planning.Validate();
        Assert.Contains(validation.Issues, issue => issue.Message.Contains("mapping disagrees", StringComparison.Ordinal));
        Assert.Contains(validation.Issues, issue => issue.Message.Contains("Squall", StringComparison.Ordinal));
    }

    [Fact]
    public void SchemaRejectsMissingSecondExecutionUnknownFieldsAndDuplicateFields()
    {
        (EvidenceStore store, CampaignPlanning planning) = Create();
        EffectPlan plan = store.Read<EffectPlan>("effects/synthetic-effect/plan.json");
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateRecord(plan with { Cases = [plan.Cases[0] with { Executions = 1 }] }, "effect-plan"));
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes("{\"schema_version\":1,\"schema_version\":1}"), "profile"));
        Assert.Throws<InvalidDataException>(() => SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes("{\"unexpected\":true}"), "profile"));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("nested/../../outside")]
    [InlineData("./relative")]
    public void EvidenceCannotEscapeStore(string relative)
    {
        EvidenceStore store = new(directory);
        Assert.Throws<ArgumentException>(() => store.Resolve(relative));
    }

    [Fact]
    public void StoreMustBeAbsoluteOutsideCheckoutAndInputRevisionsAreNewDirectories()
    {
        Assert.Throws<ArgumentException>(() => new EvidenceStore("relative"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".git"), "gitdir: elsewhere");
        Assert.Throws<ArgumentException>(() => new EvidenceStore(Path.Combine(directory, "campaign")));
    }

    [Fact]
    public void BytesBeyondFourGiBRetainIntegerPrecisionInSchemaAndRecords()
    {
        (EvidenceStore store, _) = Create();
        BuildRecord build = store.Read<BuildRecord>("build.json");
        Assert.Equal(9_007_199_254_740_993L, build.Files[0].Bytes);
        SchemaValidation.ValidateBytes(store.ReadBytes("build.json"), "build");
    }

    [Fact]
    public void NewPassRejectsInvalidBuildIdentityWithoutPublishingIt()
    {
        (EvidenceStore store, CampaignPlanning planning) = Create();
        planning.Seal();
        BuildRecord build = store.Read<BuildRecord>("build.json");
        Assert.Throws<InvalidDataException>(() => planning.CreatePass("invalid", "final", build with { SourceCommit = "invalid" }, null));
        Assert.False(store.Exists("passes/invalid.json"));
        Assert.Throws<InvalidDataException>(() => planning.CreatePass("invalid", "final", build with { Dirty = true }, null));
    }

    [Fact]
    public void ReadingPassRequiresItsOriginalBuildManifest()
    {
        (EvidenceStore store, CampaignPlanning planning) = Create();
        planning.Seal();
        PassRecord pass = planning.CreatePass("final", "final", store.Read<BuildRecord>("build.json"), null);
        PassRecord read = planning.ReadPass("final");
        Assert.Equal(pass.BuildHash, read.BuildHash);
        Assert.Equal(pass.RequiredCases, read.RequiredCases);
        File.Delete(Path.Combine(store.Root, "records", "builds", pass.BuildHash + ".json.record.json"));
        Assert.Throws<FileNotFoundException>(() => planning.ReadPass("final"));
    }

    private (EvidenceStore Store, CampaignPlanning Planning) Create(Func<InventoryEntry, InventoryEntry>? entryTransform = null, Func<CaseRecipe, CaseRecipe>? caseTransform = null)
    {
        EvidenceStore store = new(directory);
        string hash = new('a', 64);
        BuildFile file = new("synthetic.bin", 9_007_199_254_740_993L, hash);
        ResourceRecord resources = new(1, "synthetic", [file], []);
        BuildRecord build = new(1, new('a', 40), false, null, "Release", [file]);
        PerformanceProfile profile = new(1, "synthetic", 1920, 1080, "synthetic-gpu", 60, "muted", "windowed", "off", "60", "none", "synthetic", []);
        InventoryEntry entry = new("synthetic-entry", "synthetic-action", "synthetic-effect", 1, "scheduled", ["synthetic-case"], ["synthetic test mapping"], null);
        ScenarioRecipe scenario = new(1, "synthetic", 1, "Squall", "synthetic-target", ["synthetic-target"], ["synthetic-resource"], ["synthetic-slot"], "synthetic", [], "synthetic initialization", [], ["synthetic mapping"], ["synthetic legality"]);
        InvocationRecipe invocation = new("synthetic", "Squall", "synthetic", [], "advance naturally", "synthetic seed", false, []);
        CaseRecipe recipe = new(1, "synthetic-case", "synthetic-effect", "synthetic-action", 1, "canonical", "player", scenario, invocation, 2, [], "nonterminal", ["synthetic milestone"], ["synthetic drain"], ["synthetic invariant"], ["synthetic resolution"], ["synthetic counters"], new(100, 100, 100, 100, 1000, "synthetic bounds"), []);
        CampaignPlanning planning = new(store);
        planning.Create("synthetic-campaign", 1, null, new(1, EvidenceStore.HashRecord(resources), [entryTransform?.Invoke(entry) ?? entry]), build, profile, resources, [caseTransform?.Invoke(recipe) ?? recipe]);
        return (store, planning);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
