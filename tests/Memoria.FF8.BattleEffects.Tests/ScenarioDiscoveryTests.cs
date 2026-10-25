using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioDiscoveryTests
{
    [Fact]
    public void InstalledOutputIsRejectedBeforeResourceReads()
    {
        string game = Path.Combine(Path.GetTempPath(), "synthetic-game-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<ArgumentException>(() => ScenarioDiscovery.Generate(game, Path.Combine(game, "results")));
    }

    [Fact]
    public void RepositoryOutputIsRejectedBeforeResourceReads()
    {
        string repository = Path.Combine(Path.GetTempPath(), "synthetic-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repository, ".git"));
        try
        {
            Assert.Throws<ArgumentException>(() => ScenarioDiscovery.Generate(Path.Combine(repository, "missing-game"), Path.Combine(repository, "results")));
        }
        finally
        {
            Directory.Delete(repository, true);
        }
    }

    [Fact]
    public void EveryBindingRowIsRequiredIncludingNullRows()
    {
        InstalledScenarioInventory inventory = Inventory();
        ScenarioDiscovery.ValidateCoverage(inventory, []);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory with { Bindings = inventory.Bindings[..399] }, []));
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory with { Bindings = inventory.Bindings.Append(inventory.Bindings[0]).ToArray() }, []));
    }

    [Fact]
    public void BindingCoverageRequiresRealMatchingScenarioReferences()
    {
        InstalledScenarioInventory inventory = Inventory();
        DiscoveredBinding[] bindings = inventory.Bindings.ToArray();
        bindings[0] = bindings[0] with { Entry = 123, Disposition = "covered_by_case", ScenarioIds = ["synthetic-case"] };
        inventory = inventory with { Bindings = bindings };
        ScenarioDiscovery.ValidateCoverage(inventory, [Scenario()]);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory, [Scenario() with { EffectId = 2 }]));
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory, []));
        bindings[0] = bindings[0] with { Disposition = "excluded_with_reason" };
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory, [Scenario()]));
    }

    [Fact]
    public void CompiledEvidenceMustMatchActualBindingAndCannotAssertPlayableSupport()
    {
        InstalledScenarioInventory inventory = Inventory();
        DiscoveredBinding[] bindings = inventory.Bindings.Select(binding => binding with { Implementation = new(binding.EffectId, binding.Entry, "unknown", []) }).ToArray();
        CompiledImplementationSummary summary = new("implementation-evidence.json", "compiled_dispatch_registration_evidence_only", new string('0', 64), new string('1', 64), 0, 0, 400, 1, false);
        inventory = inventory with { Bindings = bindings, ImplementationSummary = summary };
        ScenarioDiscovery.ValidateCoverage(inventory, []);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory with { ImplementationSummary = summary with { PlayableSupportEstablished = true } }, []));
        bindings[0] = bindings[0] with { Implementation = new(2, 0, "unknown", []) };
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(inventory, []));
    }

    [Fact]
    public void BlockedScenarioCannotLoseItsBlockerOrClaimAcceptance()
    {
        DiscoveredScenario scenario = Scenario();
        ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario]);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario with { ExecutionBlockers = [] }]));
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario with { CampaignAcceptance = true }]));
    }

    [Fact]
    public void RunnableDiagnosticNeedsRecipeAndNoExecutionBlockers()
    {
        DiscoveredScenario scenario = Scenario() with { Status = "RUNNABLE", DiagnosticRecipe = "external.recipe.json", ExecutionBlockers = [] };
        ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario]);
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario with { DiagnosticRecipe = null }]));
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario with { ExecutionBlockers = ["missing owner"] }]));
        Assert.Throws<InvalidDataException>(() => ScenarioDiscovery.ValidateCoverage(Inventory(), [scenario, scenario]));
    }

    private static InstalledScenarioInventory Inventory() => new(1, new string('0', 64), new string('1', 64), new string('2', 64), DateTimeOffset.UnixEpoch, Enumerable.Range(1, 400).Select(effect => new DiscoveredBinding(effect, 0, 0, [], "excluded_with_reason", "Synthetic null binding.", [])).ToArray(), [], [], []);

    private static DiscoveredScenario Scenario() => new("synthetic-case", 1, "synthetic-action", "magic", "PLANNED_BLOCKED", "unresolved", false, null, null, null, "Squall", null, 2, 1, null, null, "Unresolved", [], [], 2, [], ["Synthetic mapping"], ["Missing fixture"], ["Missing phase"], []);
}
