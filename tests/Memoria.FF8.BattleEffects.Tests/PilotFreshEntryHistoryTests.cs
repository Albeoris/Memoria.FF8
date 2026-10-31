using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotFreshEntryHistoryTests
{
    [Fact]
    public void Authenticated_pair_resumes_and_recalculates_two_explicit_independent_cast_sources()
    {
        using History fixture = new();
        PilotResumePlan plan = PilotResume.Plan(fixture.Manifest, fixture.Root);
        Assert.Empty(plan.PendingCases);
        PilotReusedCase reused = Assert.Single(plan.ReusedCases);
        Assert.True(reused.Tested);
        Assert.Null(reused.TelemetryPath);
        Assert.Null(reused.CandidatePath);
        Assert.Equal(fixture.Pins.CandidateFingerprint, reused.PreviousBuildFingerprint);
        PilotFpsCase measured = Assert.Single(PilotFpsRecalculation.Recalculate(fixture.Manifest, fixture.Root, 1000).Report.Cases);
        Assert.Equal("fresh-entry-pair", measured.ExecutionProtocol);
        Assert.Equal(fixture.PinsPath, measured.PairPinsPath);
        Assert.Null(measured.TelemetryPath);
        Assert.Null(measured.CandidatePath);
        Assert.Equal(2, measured.Casts.Length);
        for (int index = 0; index < 2; index++)
        {
            Assert.Equal(fixture.Pins.Entries[index], measured.Casts[index].Source);
        }
        Assert.False(File.Exists(Path.Combine(fixture.CaseDirectory, "output", "telemetry-report.json")));
    }

    [Theory]
    [InlineData("report")]
    [InlineData("initialization")]
    [InlineData("pins")]
    [InlineData("build")]
    [InlineData("protocol")]
    [InlineData("fingerprint")]
    public void Changed_or_malformed_pair_becomes_pending_without_reusing_a_pass(string fault)
    {
        using History fixture = new();
        switch (fault)
        {
            case "report":
                File.AppendAllText(fixture.Pins.Entries[1].Report.Path, " ");
                break;
            case "initialization":
                File.AppendAllText(fixture.Pins.Entries[1].Initialization.Path, " ");
                break;
            case "pins":
                File.WriteAllText(fixture.PinsPath, "{");
                break;
            case "build":
                File.AppendAllText(Path.Combine(fixture.Pins.CandidateDirectory, "Memoria.FF8.dll"), "changed");
                break;
            case "protocol":
                Write(fixture.ResultPath, fixture.Result with { ExecutionProtocol = "same-battle-repeat" });
                break;
            case "fingerprint":
                Write(Path.Combine(fixture.Root, "attempt", "manifest.json"), fixture.Manifest with { BuildFingerprint = new string('0', 64) });
                break;
        }
        PilotResumePlan plan = PilotResume.Plan(fixture.Manifest, fixture.Root);
        Assert.Single(plan.PendingCases);
        Assert.Empty(plan.ReusedCases);
        Assert.NotEmpty(plan.Diagnostics);
        Assert.Empty(PilotFpsRecalculation.Recalculate(fixture.Manifest, fixture.Root, 30).Report.Cases);
    }

    private static void Write<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, RecordJson.Options));

    private sealed class History : IDisposable
    {
        private readonly FreshEntryPairValidationTests.Fixture source;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "memoria-pair-history-" + Guid.NewGuid().ToString("N"));
        public string CaseDirectory { get; }
        public string ResultPath => Path.Combine(CaseDirectory, "pilot-result.json");
        public string PinsPath => Path.Combine(CaseDirectory, "fresh-entry-pair-pins.json");
        public FreshEntryPairPins Pins { get; }
        public PilotCaseSummary Result { get; }
        public PilotManifest Manifest { get; }

        public History()
        {
            source = new(FreshEntryPairValidationTests.ActualExecutable());
            FreshEntryPairPins original = source.Pins();
            CaseDirectory = Path.Combine(Root, "attempt", "01-one");
            Directory.CreateDirectory(CaseDirectory);
            ImmutableArray<FreshEntryRunPins>.Builder entries = ImmutableArray.CreateBuilder<FreshEntryRunPins>();
            for (int index = 0; index < 2; index++)
            {
                string output = Path.Combine(CaseDirectory, $"entry-{index + 1}", "output");
                Directory.CreateDirectory(output);
                string report = Path.Combine(output, "telemetry-report.json");
                string initialization = Path.Combine(output, "fresh-entry-initialization.json");
                File.Copy(original.Entries[index].Report.Path, report);
                File.Copy(original.Entries[index].Initialization.Path, initialization);
                entries.Add(new(original.Entries[index].Report with { Path = report }, original.Entries[index].Initialization with { Path = initialization }));
            }
            Pins = original with { Entries = entries.ToImmutable() };
            Write(PinsPath, Pins);
            int effect = source.Reports[0]["expected_effect"]!.GetValue<int>();
            PilotCase item = new("one", Pins.Recipe.Path, EffectId: effect, RecipeSha256: Pins.Recipe.Sha256);
            Manifest = new(1, "unused", "unused", "unused", "unused", new(1, 1, 1, 1, 1, 1, 1), [item], Pins.CandidateFingerprint);
            Write(Path.Combine(Root, "attempt", "manifest.json"), Manifest);
            FreshEntryPairVerdict validation = FreshEntryPairValidation.ValidateFiles(Pins);
            Assert.True(validation.FunctionalPass, string.Join("; ", validation.Errors));
            double effectMs = validation.Entries.Sum(entry => entry.EffectDurationsMs.Sum());
            double windows = validation.Entries.Sum(entry => entry.Executions.Sum(execution => execution.DurationMs));
            string[] errors = validation.Entries.SelectMany(entry => entry.Errors).Distinct().ToArray();
            Result = PilotRunner.Summarize("one", errors.Length == 0 ? "PASS" : "FAIL", errors, 2, windows + 100, effectMs, CaseDirectory, true, windows) with { EffectId = effect, ExecutionProtocol = "fresh-entry-pair", PairPinsPath = PinsPath };
            Write(ResultPath, Result);
        }

        public void Dispose()
        {
            source.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
