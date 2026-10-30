using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class FreshEntryPairValidationTests
{
    [Fact]
    public void Two_independent_source_bound_single_use_entries_pass_without_claiming_campaign_acceptance()
    {
        using Fixture fixture = new(ActualExecutable());
        var result = FreshEntryPairValidation.ValidateFiles(fixture.Pins());
        Assert.True(result.FunctionalPass, string.Join(";", result.Errors));
        Assert.Equal(2, result.Entries.Length);
        Assert.All(result.Entries, entry => Assert.Single(entry.Executions));
        Assert.False(result.CampaignAcceptance);
        using var report = JsonDocument.Parse(fixture.Reports[0].ToJsonString());
        Assert.Contains(LiveEvidenceValidation.Validate(report.RootElement).Errors, error => error.StartsWith("REPORT_INVALID:", StringComparison.Ordinal));
        Assert.Equal("PASS", LiveEvidenceValidation.ValidateFreshEntry(report.RootElement).Verdict);
    }

    [Theory]
    [InlineData("same-report")]
    [InlineData("process")]
    [InlineData("lifecycle")]
    [InlineData("battle")]
    [InlineData("recipe")]
    [InlineData("source")]
    [InlineData("build")]
    [InlineData("initialization")]
    [InlineData("dispatch")]
    [InlineData("completion")]
    [InlineData("restore")]
    [InlineData("second-use")]
    [InlineData("repair")]
    public void Pair_rejects_owner_source_recipe_native_and_independent_anchor_divergence(string fault)
    {
        using Fixture fixture = new(ActualExecutable());
        JsonObject second = fixture.Reports[1];
        switch (fault)
        {
            case "process":
                second["initialization"]!["fresh_entry_host"] = fixture.Reports[0]["initialization"]!["fresh_entry_host"]!.DeepClone();
                break;
            case "lifecycle":
                foreach (string name in new[] { "start_owners", "end_owners" })
                {
                    second["executions"]![0]![name]!["retained_memory_owner_id"] = fixture.Reports[0]["executions"]![0]![name]!["retained_memory_owner_id"]!.DeepClone();
                }
                break;
            case "battle":
                second["battle_instance_id"] = fixture.Reports[0]["battle_instance_id"]!.DeepClone();
                second["initialization"]!["battle_instance_id"] = second["battle_instance_id"]!.DeepClone();
                break;
            case "recipe":
                second["initialization"]!["preparation"]!["driver"]!["expected_effect"] = 2;
                second["expected_effect"] = 2;
                break;
            case "source":
                second["initialization"]!["fresh_entry_host"]!["source_files"]![0]!["sha256"] = new string('f', 64);
                break;
            case "build":
                File.AppendAllText(Path.Combine(fixture.Build, "Memoria.FF8.Runtime.dll"), "changed");
                break;
            case "dispatch":
                second["executions"]![0]!["dispatches"]![0]!["executed_effect"] = 2;
                break;
            case "completion":
                second["executions"]![0]!["cleanup_completed"] = false;
                break;
            case "restore":
                second["executions"]![0]!["start_owners"]!["restore_count"] = 1;
                break;
            case "second-use":
                second["executions"]!.AsArray().Add(second["executions"]![0]!.DeepClone());
                break;
            case "repair":
                second["initialization"]!["preparation"]!["driver"]!["repairs"]!.AsArray().Add(new JsonObject { ["actor_id"] = "party0", ["hit_points"] = 500 });
                break;
        }
        var pins = fixture.Pins(preserveInitialization: fault == "initialization");
        if (fault == "initialization")
        {
            second["initialization"]!["stage"] = 999;
            pins = fixture.Pins(preserveInitialization: true);
        }
        if (fault == "same-report")
        {
            pins = pins with { Entries = [pins.Entries[0], pins.Entries[0]] };
        }
        Assert.False(FreshEntryPairValidation.ValidateFiles(pins).FunctionalPass);
    }

    [Fact]
    public void Existing_same_battle_one_window_is_not_implicitly_promoted()
    {
        JsonObject report = LiveEvidenceValidationTests.Report();
        report["executions"]!.AsArray().RemoveAt(1);
        using var document = JsonDocument.Parse(report.ToJsonString());
        Assert.Contains("EXACTLY_TWO_EXECUTIONS_REQUIRED", LiveEvidenceValidation.Validate(document.RootElement).Errors);
        Assert.NotEqual("PASS", LiveEvidenceValidation.ValidateFreshEntry(document.RootElement).Verdict);
    }

    [Fact]
    public void Coherently_replaced_EXE_and_initialization_cannot_bypass_actual_execution_stack_source()
    {
        using Fixture fixture = new();
        var result = FreshEntryPairValidation.ValidateFiles(fixture.Pins());
        Assert.False(result.FunctionalPass);
        Assert.Contains(result.Errors, error => error.Contains("Execution-stack source names a different original executable", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("source")]
    [InlineData("recipe")]
    [InlineData("dispatch")]
    [InlineData("completion")]
    [InlineData("restore")]
    [InlineData("second-use")]
    [InlineData("repair")]
    public void Pure_single_entry_structure_still_rejects_divergence_without_installed_inputs(string fault)
    {
        using Fixture fixture = new();
        JsonObject report = fixture.Reports[0];
        switch (fault)
        {
            case "owner":
                report["executions"]![0]!["start_owners"]!["retained_memory_owner_id"] = Guid.NewGuid().ToString();
                break;
            case "source":
                report["initialization"]!["fresh_entry_host"]!["source_files"]![0]!["sha256"] = new string('f', 64);
                break;
            case "recipe":
                report["initialization"]!["preparation"]!["driver"]!["expected_effect"] = 2;
                break;
            case "dispatch":
                report["executions"]![0]!["dispatches"]![0]!["executed_effect"] = 2;
                break;
            case "completion":
                report["executions"]![0]!["cleanup_completed"] = false;
                break;
            case "restore":
                report["executions"]![0]!["start_owners"]!["restore_count"] = 1;
                break;
            case "second-use":
                report["executions"]!.AsArray().Add(report["executions"]![0]!.DeepClone());
                break;
            case "repair":
                report["initialization"]!["preparation"]!["driver"]!["repairs"]!.AsArray().Add(new JsonObject { ["actor_id"] = "party0", ["hit_points"] = 500 });
                break;
        }
        using var document = JsonDocument.Parse(report.ToJsonString());
        Assert.NotEqual("PASS", LiveEvidenceValidation.ValidateFreshEntry(document.RootElement).Verdict);
    }

    private static string ActualExecutable()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_EXECUTABLE");
        Assert.SkipUnless(path is not null && File.Exists(path), "Select the existing authenticated original executable for full-file pair source validation.");
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash.ToLowerInvariant(), Hash(File.ReadAllBytes(path!)));
        return path!;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-entry-pair-" + Guid.NewGuid().ToString("N"));
        private readonly FreshEntryFilePin recipe;
        private readonly FreshEntryFilePin executable;
        private readonly FreshEntryFilePin stack;
        private readonly string fingerprint;
        public string Build { get; }
        public JsonObject[] Reports { get; } = new JsonObject[2];

        public Fixture(string? actualExecutable = null)
        {
            Directory.CreateDirectory(root);
            Build = Path.Combine(root, "build");
            Directory.CreateDirectory(Build);
            File.WriteAllText(Path.Combine(Build, "Memoria.FF8.dll"), "synthetic managed host fixture");
            File.WriteAllText(Path.Combine(Build, "Memoria.FF8.Runtime.dll"), "synthetic runtime fixture");
            fingerprint = Hash(JsonSerializer.SerializeToUtf8Bytes(EvidenceStore.DescribeFiles(Build), RecordJson.Options));
            executable = actualExecutable is null ? Write("original.bin", "synthetic mismatching source fixture") : Pin(actualExecutable);
            NativeBattleExecutionStack profile = new(0x200100, 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('a', 64), 1, 2);
            stack = Write("stack.json", JsonSerializer.Serialize(profile));
            var source = Write("container.bin", "synthetic installed container fixture");
            var declaration = new DiagnosticFreshEntryDeclaration(DiagnosticFreshEntryDeclaration.SingleUse, [new("container.bin", source.Sha256)]);
            JsonObject template = LiveEvidenceValidationTests.Report();
            JsonObject driver = new()
            {
                ["input"] = template["input"]!.DeepClone(), ["expected_effect"] = 1,
                ["expected_native_action"] = null, ["expected_native_command"] = null,
                ["fresh_entry"] = JsonSerializer.SerializeToNode(declaration, RecordJson.Options),
                ["repairs"] = new JsonArray(), ["supporting_effects"] = new JsonArray(), ["supporting_invocations"] = new JsonArray()
            };
            JsonObject preparation = new() { ["driver"] = driver };
            recipe = Write("recipe.json", preparation.ToJsonString());
            for (int index = 0; index < 2; index++)
            {
                JsonObject report = LiveEvidenceValidationTests.Report();
                report["executions"]!.AsArray().RemoveAt(1);
                string battle = Guid.NewGuid().ToString();
                string lifecycle = Guid.NewGuid().ToString();
                report["battle_instance_id"] = battle;
                report["fresh_entry"] = JsonSerializer.SerializeToNode(declaration, RecordJson.Options);
                report["supporting_effects"] = new JsonArray();
                report["supporting_invocations"] = new JsonArray();
                report["declared_repairs"] = new JsonArray();
                report["declared_terminal_hold"] = null;
                foreach (string name in new[] { "start_owners", "end_owners" })
                {
                    report["executions"]![0]![name]!["battle_instance_id"] = battle;
                    report["executions"]![0]![name]!["retained_memory_owner_id"] = lifecycle;
                }
                var host = new DiagnosticFreshEntryHostReceipt(Guid.Parse(battle), Guid.Parse(lifecycle), Guid.NewGuid(), 100 + index, 1000 + index, Hash(File.ReadAllBytes(Path.Combine(Build, "Memoria.FF8.dll"))), Hash(File.ReadAllBytes(Path.Combine(Build, "Memoria.FF8.Runtime.dll"))), Hash(JsonSerializer.SerializeToUtf8Bytes(profile)), declaration.SourceFiles);
                report["initialization"] = new JsonObject
                {
                    ["battle_instance_id"] = battle, ["initializer_version"] = "direct-new-game-v2", ["executable_sha256"] = executable.Sha256,
                    ["preparation"] = preparation.DeepClone(), ["fresh_entry_host"] = JsonSerializer.SerializeToNode(host, RecordJson.Options),
                    ["ordered_stages"] = new JsonArray("installed-cold-process", "canonical-encounter-construction", "resource-derived-new-game")
                };
                Reports[index] = report;
                Write($"initialization-{index}.json", report["initialization"]!.ToJsonString());
            }
        }

        public FreshEntryPairPins Pins(bool preserveInitialization = false)
        {
            ImmutableArray<FreshEntryRunPins>.Builder entries = ImmutableArray.CreateBuilder<FreshEntryRunPins>();
            for (int index = 0; index < 2; index++)
            {
                var report = Write($"report-{index}.json", Reports[index].ToJsonString());
                var initialization = preserveInitialization ? Pin(Path.Combine(root, $"initialization-{index}.json")) : Write($"initialization-{index}.json", Reports[index]["initialization"]!.ToJsonString());
                entries.Add(new(report, initialization));
            }
            return new(recipe, executable, stack, root, Build, fingerprint, entries.ToImmutable());
        }

        private FreshEntryFilePin Write(string name, string text)
        {
            string path = Path.Combine(root, name);
            File.WriteAllText(path, text);
            return Pin(path);
        }
        private static FreshEntryFilePin Pin(string path) => new(path, Hash(File.ReadAllBytes(path)));
        public void Dispose() => Directory.Delete(root, true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
