using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class EffectCoverageCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-effect-coverage-tests-" + Guid.NewGuid().ToString("N"));

    public EffectCoverageCommandTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public void Preparation_publishes_concrete_missing_effects_without_counting_selected_cases_as_completed()
    {
        (string manifest, string catalog) = Inputs();
        string output = Path.Combine(root, "preparation.json");
        Assert.Equal(0, Program.Main(["reconcile-coverage", "--input", manifest, "--catalog", catalog, "--output", output]));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(output));
        JsonElement coverage = document.RootElement.GetProperty("coverage");
        Assert.Equal("preparation", document.RootElement.GetProperty("phase").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("authenticated_cases").GetInt32());
        Assert.Equal(1, coverage.GetProperty("runnable").GetProperty("count").GetInt32());
        Assert.Equal(0, coverage.GetProperty("completed").GetProperty("count").GetInt32());
        Assert.Equal(new[] { 1, 2 }, coverage.GetProperty("missing").GetProperty("effect_ids").EnumerateArray().Select(id => id.GetInt32()));
    }

    [Fact]
    public void Explicit_full_execution_gate_fails_but_keeps_the_reviewable_coverage_report()
    {
        (string manifest, string catalog) = Inputs();
        string history = Path.Combine(root, "history");
        Directory.CreateDirectory(history);
        string output = Path.Combine(root, "execution.json");
        Assert.Equal(1, Program.Main(["reconcile-coverage", "--input", manifest, "--catalog", catalog, "--history", history, "--output", output, "--require-full", "true"]));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(output));
        Assert.Equal("authenticated_execution", document.RootElement.GetProperty("phase").GetString());
        Assert.False(document.RootElement.GetProperty("coverage").GetProperty("full_execution_complete").GetBoolean());
        Assert.Equal(0, document.RootElement.GetProperty("coverage").GetProperty("completed").GetProperty("count").GetInt32());
    }

    [Fact]
    public void A_success_summary_without_authenticated_cast_receipts_does_not_create_coverage()
    {
        (string manifestPath, string catalog) = Inputs();
        PilotManifest manifest = RecordJson.Read<PilotManifest>(manifestPath);
        string history = Path.Combine(root, "history");
        string attempt = Path.Combine(history, "attempt");
        Directory.CreateDirectory(attempt);
        Write(Path.Combine(attempt, "manifest.json"), manifest);
        string caseDirectory = Path.Combine(attempt, "01-one");
        Directory.CreateDirectory(caseDirectory);
        Write(Path.Combine(caseDirectory, "pilot-result.json"), new PilotCaseSummary("one", "PASS", [], 2, 1, 1, 1, 0, true, caseDirectory, true, EffectId: 1));
        string output = Path.Combine(root, "unproved.json");
        Assert.Equal(1, Program.Main(["reconcile-coverage", "--input", manifestPath, "--catalog", catalog, "--history", history, "--output", output, "--require-full", "true"]));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(output));
        Assert.Equal(0, document.RootElement.GetProperty("authenticated_cases").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("coverage").GetProperty("completed").GetProperty("count").GetInt32());
        Assert.NotEmpty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
    }

    private (string Manifest, string Catalog) Inputs()
    {
        string recipe = Path.Combine(root, "recipe.json");
        Write(recipe, new { encounter = 94, driver = new { expected_effect = 1 } });
        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(recipe)));
        PilotManifest manifest = new(1, Path.Combine(root, "host.exe"), root, root, Path.Combine(root, "results"), new(1, 1, 1, 1, 1, 1, 1), [new("one", recipe, EffectId: 1, RecipeSha256: hash), new("two", null, "PLANNED_BLOCKED", ["Unresolved invocation owner."], 2)]);
        InstalledScenarioInventory catalog = new(1, new string('0', 64), new string('0', 64), new string('0', 64), DateTimeOffset.UnixEpoch, [Binding(1, 0x1000), Binding(2, 0x2000), Binding(3, 0)], [], [], []);
        string manifestPath = Path.Combine(root, "manifest-input.json");
        string catalogPath = Path.Combine(root, "catalog.json");
        Write(manifestPath, manifest);
        Write(catalogPath, catalog);
        return (manifestPath, catalogPath);
    }

    private static DiscoveredBinding Binding(int id, uint entry) => new(id, entry, 0, [], "unresolved", "test catalog binding", []);

    private static void Write<T>(string path, T value) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, RecordJson.Options));

    public void Dispose()
    {
        Directory.Delete(root, true);
    }
}
