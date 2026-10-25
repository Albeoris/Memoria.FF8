using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotSlowEffectsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-slow-query-tests-" + Guid.NewGuid().ToString("N"));
    private readonly PilotManifest requested;

    public PilotSlowEffectsTests()
    {
        Directory.CreateDirectory(root);
        requested = new(1, "unused", "unused", "unused", "unused", new(1, 1, 1, 1, 1, 1, 1), [Case("one", 1)], "new-build");
    }

    [Theory]
    [InlineData(10, 40, 25, false)]
    [InlineData(10, 40, 25.001, true)]
    [InlineData(40, 10, 30, true)]
    [InlineData(10, 40, 30, true)]
    public void Either_cast_qualifies_with_strict_cutoff(int firstInterval, int secondInterval, double cutoff, bool expected)
    {
        Attempt("01", requested, [(firstInterval, secondInterval)]);

        PilotSlowEffectsReport result = PilotSlowEffects.Query(requested, root, cutoff);

        Assert.Equal(1, result.CompletedCases);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(expected ? 1 : 0, result.Effects.Length);
        if (expected)
        {
            PilotSlowCase slow = Assert.Single(Assert.Single(result.Effects).Cases);
            Assert.Equal(new[] { 1000.0 / firstInterval, 1000.0 / secondInterval }, slow.MedianFps);
            Assert.Equal(25, slow.MinimumCastMedianFps);
            Assert.Equal("old-build", slow.SourceBuildFingerprint);
            Assert.Equal("one", slow.Case.Id);
            Assert.Equal(requested.Cases[0].RecipeSha256, slow.Case.RecipeSha256);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(slow.SourceManifestPath))), slow.SourceManifestSha256);
            Assert.Equal(21, slow.ActionIdentity.GetProperty("action_id").GetInt32());
        }
    }

    [Fact]
    public void Groups_all_slow_scenarios_by_effect_and_orders_by_worst_cast()
    {
        PilotManifest manifest = requested with { Cases = [requested.Cases[0], Case("two", 1), Case("three", 2), Case("fast", 1)] };
        Attempt("01", manifest, [(40, 10), (50, 10), (80, 10), (10, 10)]);

        PilotSlowEffectsReport result = PilotSlowEffects.Query(manifest, root, 30);

        Assert.Equal(4, result.CompletedCases);
        Assert.Equal(new[] { 2, 1 }, result.Effects.Select(effect => effect.EffectId));
        Assert.Equal(12.5, result.Effects[0].MinimumCastMedianFps);
        Assert.Equal(new[] { "two", "one" }, result.Effects[1].Cases.Select(item => item.Case.Id));
    }

    [Fact]
    public void Latest_authoritative_scenario_is_returned_once()
    {
        string older = Attempt("01", requested, [(80, 10)]);
        string latest = Attempt("02", requested, [(40, 10)]);
        File.SetLastWriteTimeUtc(Path.Combine(older, "manifest.json"), DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(Path.Combine(latest, "manifest.json"), DateTime.UtcNow.AddMinutes(-1));

        PilotSlowCase slow = Assert.Single(Assert.Single(PilotSlowEffects.Query(requested, root, 30).Effects).Cases);

        Assert.Equal(25, slow.MinimumCastMedianFps);
        Assert.Equal(Path.Combine(latest, "manifest.json"), slow.SourceManifestPath);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("incomplete")]
    [InlineData("digest")]
    public void Invalid_latest_evidence_is_excluded_without_falling_back(string kind)
    {
        string older = Attempt("01", requested, [(80, 10)]);
        string latest = Attempt("02", requested, [(40, 10)]);
        File.SetLastWriteTimeUtc(Path.Combine(older, "manifest.json"), DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(Path.Combine(latest, "manifest.json"), DateTime.UtcNow.AddMinutes(-1));
        string resultPath = Path.Combine(latest, "01-one", "pilot-result.json");
        if (kind == "malformed")
        {
            File.WriteAllText(resultPath, "{");
        }
        else if (kind == "incomplete")
        {
            Write(resultPath, Read<PilotCaseSummary>(resultPath) with { Executions = 1 });
        }
        else
        {
            File.AppendAllText(Path.Combine(latest, "01-one", "output", "telemetry-report.json"), " ");
        }

        PilotSlowEffectsReport result = PilotSlowEffects.Query(requested, root, 30);

        Assert.Empty(result.Effects);
        Assert.Equal(0, result.CompletedCases);
        Assert.Equal(1, result.PendingCases);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Finalized_error_and_blocked_cases_are_counted_and_excluded()
    {
        PilotCase blocked = new("blocked", null, "PLANNED_BLOCKED", ["FIXTURE_UNAVAILABLE"], 2);
        PilotManifest manifest = requested with { Cases = [requested.Cases[0], blocked] };
        string attempt = Attempt("01", requested, [(40, 10)]);
        Write(Path.Combine(attempt, "manifest.json"), manifest);
        string resultPath = Path.Combine(attempt, "01-one", "pilot-result.json");
        Write(resultPath, Read<PilotCaseSummary>(resultPath) with { Verdict = "ERROR", Errors = ["HOST_FAILED"] });
        Directory.CreateDirectory(Path.Combine(attempt, "02-blocked"));
        Write(Path.Combine(attempt, "02-blocked", "pilot-result.json"), PilotRunner.BlockedResult(blocked, "unused") with { EffectId = 2, ScenarioStatus = blocked.Status });

        PilotSlowEffectsReport result = PilotSlowEffects.Query(manifest, root, 30);

        Assert.Empty(result.Effects);
        Assert.Equal(1, result.IgnoredErrors);
        Assert.Equal(1, result.IgnoredBlocked);
        Assert.Equal(0, result.CompletedCases);
        Assert.Equal(0, result.PendingCases);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Threshold_must_be_finite_and_positive(double threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PilotSlowEffects.Query(requested, root, threshold));
    }

    private PilotCase Case(string id, int effect)
    {
        string path = Path.Combine(root, id + "-recipe.json");
        Write(path, new { driver = new { expected_effect = effect, input = new { kind = "Action", actor_id = "party0", target_id = "party0", ability = "Magic", action_id = 21 } } });
        return new(id, path, EffectId: effect, RecipeSha256: Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
    }

    private string Attempt(string name, PilotManifest manifest, (int First, int Second)[] intervals)
    {
        string attempt = Path.Combine(root, name);
        Directory.CreateDirectory(attempt);
        Write(Path.Combine(attempt, "manifest.json"), manifest with { BuildFingerprint = "old-build" });
        for (int index = 0; index < manifest.Cases.Length; index++)
        {
            PilotCase item = manifest.Cases[index];
            string caseDirectory = Path.Combine(attempt, $"{index + 1:D2}-{item.Id}");
            string output = Path.Combine(caseDirectory, "output");
            Directory.CreateDirectory(output);
            JsonObject report = Report(item.EffectId!.Value, intervals[index].First, intervals[index].Second);
            foreach (JsonNode? execution in report["executions"]!.AsArray())
            {
                execution!["frames"]!["median_fps"] = 9999;
            }
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(report, RecordJson.Options);
            File.WriteAllBytes(Path.Combine(output, "telemetry-report.json"), bytes);
            Write(Path.Combine(output, "candidate.json"), new { status = "CANDIDATE_COMPLETED", reason = (string?)null, telemetry_report = "telemetry-report.json", telemetry_sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), battle_instance_id = report["battle_instance_id"]!.GetValue<string>() });
            LiveEvidenceVerdict validation = LiveEvidenceValidation.Validate(JsonSerializer.SerializeToElement(report));
            PilotCaseSummary summary = PilotRunner.Summarize(item.Id, validation.Verdict, validation.Errors, 2, 200, 12, "unused", true, validation.Executions.Sum(execution => execution.DurationMs));
            Write(Path.Combine(caseDirectory, "pilot-result.json"), summary with { EffectId = item.EffectId });
        }
        return attempt;
    }

    private static JsonObject Report(int effect, int firstInterval, int secondInterval)
    {
        string battle = "b8de1951-3be9-4360-85ac-9238406b20c8";
        object[] executions = Enumerable.Range(0, 2).Select(index =>
        {
            long callback = 10 + index * 10;
            long frame = index * 10;
            long ticks = 100 + index * 100;
            int hp = 100 + index * 100;
            object State(int health, int stock) => new { id = "party0", current_hp = health, maximum_hp = 500, statuses0 = 0, statuses1 = 0, magic_stocks = new[] { new { magic_id = 21, quantity = stock } } };
            TelemetryOwnerState Owners(long cb, long fr) => new(battle, 1, "memory", "renderer", 1, 0, 0, cb, fr);
            TelemetryFrameCollector frames = new(ticks, 1000, 2);
            int interval = index == 0 ? firstInterval : secondInterval;
            long first = ticks + interval;
            frames.RecordPresentation(new(first, frame + 1, "effect", callback + 1, 1, 1, 0));
            frames.RecordPresentation(new(first + interval, frame + 2, "tail", callback + 2, 2, 2, 0));
            return (object)new
            {
                index = index + 1, completed = true, cleanup_completed = true, tail_presented_frames = 2,
                start_callback = callback, end_callback = callback + 2, start_frame = frame, end_frame = frame + 2, renderer_frames = 2,
                preceding_presentation_ticks = ticks, effect_started_ticks = ticks + 2, effect_completed_ticks = ticks + 8, effect_duration_ms = 6,
                start_owners = Owners(callback, frame), end_owners = Owners(callback + 2, frame + 2),
                dispatches = new[] { new { serial = index + 1, callback = callback + 1, actor_id = "party0", ability = "Magic", action_id = 21, requested_effect = effect, executed_effect = effect } },
                frames = frames.Complete(),
                events = new[] { new { kind = "heal", actor_id = "party0" }, new { kind = "ActionComplete", actor_id = "party0" } },
                resolutions = new[] { new { frame = frame + 1, callback = callback + 1, kind = "heal", actor_id = "party0", original_target_id = "party0", actual_target_id = "party0", resolved_amount = 100, actual_amount = 100, before_hp = hp, after_hp = hp + 100, hp_owner = "character" } },
                before = new[] { State(hp, 10 - index) }, after = new[] { State(hp + 100, 9 - index) }, dropped_events = 0, integrity_error = (string?)null
            };
        }).ToArray();
        return JsonSerializer.SerializeToNode(new
        {
            schema_version = 1, battle_instance_id = battle, expected_effect = effect,
            input = new { kind = "Action", actor_id = "party0", target_id = "party0", ability = "Magic", action_id = 21, draw_cast = false },
            initialization_verified = true, fixture_verified = true, fixture_blockers = Array.Empty<string>(), blocker = (string?)null,
            environment = new { renderer = "synthetic-gpu", width = 1920, height = 1080, gpu = true, real_time = true, active = true },
            audio = new { entry_receipts = 1, dispatch_receipts = 1, enabled = true }, timestamp_frequency = 1000, executions
        }, RecordJson.Options)!.AsObject();
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), RecordJson.Options)!;
    private static void Write<T>(string path, T value) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, RecordJson.Options));

    public void Dispose()
    {
        Directory.Delete(root, true);
    }
}
