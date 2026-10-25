using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class PilotResumeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-resume-tests-" + Guid.NewGuid().ToString("N"));
    private readonly PilotManifest requested;

    public PilotResumeTests()
    {
        Directory.CreateDirectory(root);
        string recipe = Path.Combine(root, "recipe.json");
        File.WriteAllText(recipe, """{"driver":{"expected_effect":1,"input":{"kind":"Action","actor_id":"party0","target_id":"party0","ability":"Magic","action_id":21}}}""");
        string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(recipe)));
        requested = new(1, "unused", "unused", "unused", "unused", new(1, 1, 1, 1, 1, 1, 1), [new("one", recipe, EffectId: 1, RecipeSha256: hash)], "new-build");
    }

    [Fact]
    public void Interrupted_batch_reuses_only_completed_cases_with_old_build_lineage()
    {
        PilotCase second = requested.Cases[0] with { Id = "two" };
        PilotManifest both = requested with { Cases = [requested.Cases[0], second] };
        Attempt("01", both);

        PilotResumePlan plan = PilotResume.Plan(both, root);

        Assert.Equal(new[] { second }, plan.PendingCases);
        Assert.Equal(1, plan.PreviouslyTested);
        Assert.Equal(1, plan.PreviouslyPassed);
        Assert.Equal("old-build", Assert.Single(plan.ReusedCases).PreviousBuildFingerprint);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Complete_pass_and_performance_fail_are_checked_and_skipped(bool slow, bool overhead)
    {
        Attempt("01", requested, slow: slow, overhead: overhead);

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Empty(plan.PendingCases);
        Assert.Equal(slow || overhead ? 1 : 0, plan.PreviouslyFailed);
        Assert.Equal(slow || overhead ? 0 : 1, plan.PreviouslyPassed);
        Assert.Equal(1, plan.PreviouslyTested);
    }

    [Theory]
    [InlineData("focus")]
    [InlineData("malformed")]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("candidate_hash")]
    [InlineData("recipe_hash")]
    [InlineData("different_recipe")]
    public void Invalid_or_unchecked_history_remains_pending(string failure)
    {
        string attempt = Attempt("01", requested);
        string result = Path.Combine(attempt, "01-one", "pilot-result.json");
        string output = Path.Combine(attempt, "01-one", "output");
        switch (failure)
        {
            case "focus":
                PilotCaseSummary old = Read<PilotCaseSummary>(result);
                Write(result, old with { Verdict = "ERROR", Errors = ["GRAPHICAL_HOST_INACTIVE"] });
                break;
            case "malformed":
                File.WriteAllText(result, "{");
                break;
            case "missing":
                File.Delete(result);
                break;
            case "incomplete":
                old = Read<PilotCaseSummary>(result);
                Write(result, old with { Executions = 1 });
                break;
            case "candidate_hash":
                JsonObject candidate = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "candidate.json")))!.AsObject();
                candidate["telemetry_sha256"] = new string('0', 64);
                Write(Path.Combine(output, "candidate.json"), candidate);
                break;
            case "recipe_hash":
                File.AppendAllText(requested.Cases[0].Recipe!, " ");
                break;
            case "different_recipe":
                PilotManifest manifest = Read<PilotManifest>(Path.Combine(attempt, "manifest.json"));
                Write(Path.Combine(attempt, "manifest.json"), manifest with { Cases = [manifest.Cases[0] with { RecipeSha256 = new string('0', 64) }] });
                break;
        }

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Single(plan.PendingCases);
        Assert.Empty(plan.ReusedCases);
        Assert.Equal(0, plan.PreviouslyTested);
    }

    [Fact]
    public void Newest_focus_error_overrides_an_older_complete_pass()
    {
        string first = Attempt("01", requested);
        string second = Attempt("02", requested);
        File.SetLastWriteTimeUtc(Path.Combine(first, "manifest.json"), DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(Path.Combine(second, "manifest.json"), DateTime.UtcNow.AddMinutes(-1));
        string path = Path.Combine(second, "01-one", "pilot-result.json");
        Write(path, Read<PilotCaseSummary>(path) with { Verdict = "ERROR", Errors = ["GRAPHICAL_HOST_INACTIVE"] });

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Single(plan.PendingCases);
        Assert.Empty(plan.ReusedCases);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    public void Finalized_other_errors_are_retained_without_testing_or_requiring_telemetry(int executions, bool processExited)
    {
        string attempt = Attempt("01", requested);
        string caseDirectory = Path.Combine(attempt, "01-one");
        string path = Path.Combine(caseDirectory, "pilot-result.json");
        Write(path, Read<PilotCaseSummary>(path) with { Verdict = "ERROR", Errors = ["HOST_START_FAILED"], Executions = executions, ProcessExited = processExited });
        Directory.Delete(Path.Combine(caseDirectory, "output"), true);

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Empty(plan.PendingCases);
        Assert.Equal(1, plan.PreviouslyErrored);
        Assert.Equal(0, plan.PreviouslyTested);
        Assert.Equal(0, plan.PreviouslyBlocked);
        Assert.Equal("ERROR", Assert.Single(plan.ReusedCases).PreviousResult.Verdict);
    }

    [Theory]
    [InlineData("errors")]
    [InlineData("candidate")]
    [InlineData("report")]
    public void Focus_failure_substrings_in_error_or_optional_blockers_require_retry(string location)
    {
        string attempt = Attempt("01", requested);
        string caseDirectory = Path.Combine(attempt, "01-one");
        string path = Path.Combine(caseDirectory, "pilot-result.json");
        Write(path, Read<PilotCaseSummary>(path) with { Verdict = "ERROR", Errors = [location == "errors" ? "SUPERVISION_FAILED: GRAPHICAL_HOST_INACTIVE: paused" : "HOST_FAILED"] });
        if (location == "candidate")
        {
            Write(Path.Combine(caseDirectory, "output", "candidate.json"), new { reason = "SUPERVISION_FAILED: GRAPHICAL_HOST_INACTIVE: paused" });
        }
        else if (location == "report")
        {
            Write(Path.Combine(caseDirectory, "output", "telemetry-report.json"), new { blocker = "SUPERVISION_FAILED: GRAPHICAL_HOST_INACTIVE: paused" });
        }

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Single(plan.PendingCases);
        Assert.Empty(plan.ReusedCases);
    }

    [Fact]
    public void Latest_other_error_supersedes_an_old_pass_and_is_retained_as_error()
    {
        string first = Attempt("01", requested);
        string second = Attempt("02", requested);
        File.SetLastWriteTimeUtc(Path.Combine(first, "manifest.json"), DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(Path.Combine(second, "manifest.json"), DateTime.UtcNow.AddMinutes(-1));
        string path = Path.Combine(second, "01-one", "pilot-result.json");
        Write(path, Read<PilotCaseSummary>(path) with { Verdict = "ERROR", Errors = ["NATIVE_ACTION_UNSUPPORTED"] });

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Empty(plan.PendingCases);
        Assert.Equal(1, plan.PreviouslyErrored);
        Assert.Equal(0, plan.PreviouslyPassed);
        Assert.Equal(path, Assert.Single(plan.ReusedCases).ResultPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Later_declaration_preserves_old_proof_until_the_scenario_is_actually_started(bool started)
    {
        string first = Attempt("01", requested);
        string second = Path.Combine(root, "02");
        Directory.CreateDirectory(second);
        Write(Path.Combine(second, "manifest.json"), requested);
        File.SetLastWriteTimeUtc(Path.Combine(first, "manifest.json"), DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(Path.Combine(second, "manifest.json"), DateTime.UtcNow.AddMinutes(-1));
        if (started)
        {
            Directory.CreateDirectory(Path.Combine(second, "01-one"));
        }

        PilotResumePlan plan = PilotResume.Plan(requested, root);

        Assert.Equal(started ? 1 : 0, plan.PendingCases.Length);
        Assert.Equal(started ? 0 : 1, plan.PreviouslyTested);
    }

    [Fact]
    public void Exact_blocked_record_is_preserved_without_counting_it_as_tested()
    {
        PilotCase blocked = new("blocked", null, "PLANNED_BLOCKED", ["FIXTURE_UNAVAILABLE"], 2);
        PilotManifest manifest = requested with { Cases = [blocked] };
        string attempt = Path.Combine(root, "01");
        Directory.CreateDirectory(Path.Combine(attempt, "01-blocked"));
        Write(Path.Combine(attempt, "manifest.json"), manifest);
        Write(Path.Combine(attempt, "01-blocked", "pilot-result.json"), PilotRunner.BlockedResult(blocked, "ignored") with { EffectId = 2, ScenarioStatus = "PLANNED_BLOCKED" });

        PilotResumePlan plan = PilotResume.Plan(manifest, root);

        Assert.Empty(plan.PendingCases);
        Assert.Equal(1, plan.PreviouslyBlocked);
        Assert.Equal(0, plan.PreviouslyTested);
        Assert.Equal(0, plan.PreviouslyPassed);
        Assert.Single(PilotResume.Plan(manifest with { Cases = [blocked with { Blockers = ["OTHER_BLOCKER"] }] }, root).PendingCases);
    }

    private string Attempt(string name, PilotManifest manifest, bool slow = false, bool overhead = false)
    {
        string attempt = Path.Combine(root, name);
        string caseDirectory = Path.Combine(attempt, "01-" + manifest.Cases[0].Id);
        string output = Path.Combine(caseDirectory, "output");
        Directory.CreateDirectory(output);
        Write(Path.Combine(attempt, "manifest.json"), manifest with { BuildFingerprint = "old-build" });
        JsonObject report = Report(slow);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(report, RecordJson.Options);
        File.WriteAllBytes(Path.Combine(output, "telemetry-report.json"), bytes);
        Write(Path.Combine(output, "candidate.json"), new { status = "CANDIDATE_COMPLETED", reason = (string?)null, telemetry_report = "telemetry-report.json", telemetry_sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), battle_instance_id = report["battle_instance_id"]!.GetValue<string>() });
        LiveEvidenceVerdict validation = LiveEvidenceValidation.Validate(JsonSerializer.SerializeToElement(report));
        PilotCaseSummary summary = PilotRunner.Summarize(manifest.Cases[0].Id, validation.Verdict, validation.Errors, 2, overhead ? 11000 : 100, 12, "untrusted-path-ignored", true, validation.Executions.Sum(item => item.DurationMs));
        Write(Path.Combine(caseDirectory, "pilot-result.json"), summary with { EffectId = 1 });
        return attempt;
    }

    private static JsonObject Report(bool slow)
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
            long first = ticks + (slow ? 40 : 10);
            frames.RecordPresentation(new(first, frame + 1, "effect", callback + 1, 1, 1, 0));
            frames.RecordPresentation(new(first + 10, frame + 2, "tail", callback + 2, 2, 2, 0));
            return (object)new
            {
                index = index + 1, completed = true, cleanup_completed = true, tail_presented_frames = 2,
                start_callback = callback, end_callback = callback + 2, start_frame = frame, end_frame = frame + 2, renderer_frames = 2,
                preceding_presentation_ticks = ticks, effect_started_ticks = ticks + 2, effect_completed_ticks = ticks + 8, effect_duration_ms = 6,
                start_owners = Owners(callback, frame), end_owners = Owners(callback + 2, frame + 2),
                dispatches = new[] { new { serial = index + 1, callback = callback + 1, actor_id = "party0", ability = "Magic", action_id = 21, requested_effect = 1, executed_effect = 1 } },
                frames = frames.Complete(),
                events = new[] { new { kind = "heal", actor_id = "party0" }, new { kind = "ActionComplete", actor_id = "party0" } },
                resolutions = new[] { new { frame = frame + 1, callback = callback + 1, kind = "heal", actor_id = "party0", original_target_id = "party0", actual_target_id = "party0", resolved_amount = 100, actual_amount = 100, before_hp = hp, after_hp = hp + 100, hp_owner = "character" } },
                before = new[] { State(hp, 10 - index) }, after = new[] { State(hp + 100, 9 - index) }, dropped_events = 0, integrity_error = (string?)null
            };
        }).ToArray();
        return JsonSerializer.SerializeToNode(new
        {
            schema_version = 1, battle_instance_id = battle, expected_effect = 1,
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
