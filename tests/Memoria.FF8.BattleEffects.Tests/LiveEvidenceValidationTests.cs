using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class LiveEvidenceValidationTests
{
    [Fact]
    public void Complete_synthetic_observations_pass_telemetry_admission_without_campaign_acceptance()
    {
        LiveEvidenceVerdict result = Validate(Report());
        Assert.Equal("PASS", result.Verdict);
        Assert.False(result.CampaignAcceptance);
        Assert.Equal(2, result.Executions.Length);
        Assert.Equal(4, result.Combined!.FrameCount);
    }

    [Fact]
    public void Slow_interval_is_fail_only_when_all_required_observations_are_valid()
    {
        JsonObject report = Report();
        JsonObject first = Execution(report, 0);
        first["frames"] = JsonSerializer.SerializeToNode(Frames(100, 0, 10, true), RecordJson.Options);
        Assert.Equal("FAIL", Validate(report).Verdict);
        report["fixture_verified"] = false;
        Assert.Equal("ERROR", Validate(report).Verdict);
    }

    [Fact]
    public void Aggregate_claims_are_recomputed_and_forged_raw_durations_are_rejected()
    {
        JsonObject report = Report();
        Execution(report, 0)["frames"]!["minimum_fps"] = 1;
        Assert.Equal("PASS", Validate(report).Verdict);
        Execution(report, 0)["frames"]!["intervals"]![0]!["duration_ms"] = 1;
        Assert.Contains("FRAME_LINEAGE_INVALID", Validate(report).Errors);
    }

    [Fact]
    public void Missing_second_dispatch_or_unconsumed_spell_is_an_error()
    {
        JsonObject report = Report();
        Execution(report, 1)["dispatches"] = new JsonArray();
        Assert.Contains("DISPATCH_COUNT_INVALID", Validate(report).Errors);
        report = Report();
        Execution(report, 1)["after"]![0]!["magic_stocks"]![0]!["quantity"] = 9;
        Assert.Contains("SPELL_STOCK_CONSUMPTION_UNVERIFIED", Validate(report).Errors);
    }

    [Fact]
    public void Reconstructed_owner_missing_coverage_and_muted_route_are_errors()
    {
        JsonObject report = Report();
        Execution(report, 1)["start_owners"]!["renderer_owner_id"] = "replacement";
        Assert.Contains("CONTINUITY_BROKEN", Validate(report).Errors);
        report = Report();
        report["coverage"] = JsonSerializer.SerializeToNode(TelemetryCoverageManifest.PreparationOnly, RecordJson.Options);
        Assert.Contains("TELEMETRY_UNAVAILABLE", Validate(report).Errors);
        report = Report();
        report["audio"]!["enabled"] = false;
        Assert.Contains("AUDIO_ROUTE_UNVERIFIED", Validate(report).Errors);
    }

    [Fact]
    public void Resolution_deltas_must_reconcile_to_retained_hp_and_capped_healing_may_be_zero()
    {
        JsonObject report = Report();
        Execution(report, 1)["resolutions"]![0]!["after_hp"] = 250;
        Assert.Contains("SEMANTIC_RESOLUTION_INVALID", Validate(report).Errors);
        report = Report();
        JsonObject second = Execution(report, 1);
        second["resolutions"]![0]!["before_hp"] = 500;
        second["resolutions"]![0]!["after_hp"] = 500;
        second["resolutions"]![0]!["actual_amount"] = 0;
        second["before"]![0]!["current_hp"] = 500;
        second["after"]![0]!["current_hp"] = 500;
        Assert.Equal("PASS", Validate(report).Verdict);
    }

    [Fact]
    public void Duplicate_properties_and_missing_required_observations_are_errors()
    {
        using JsonDocument duplicate = JsonDocument.Parse("{\"schema_version\":1,\"schema_version\":1}");
        Assert.Equal("ERROR", LiveEvidenceValidation.Validate(duplicate.RootElement).Verdict);
        Assert.Equal("ERROR", Validate(new JsonObject()).Verdict);
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }

    private static JsonObject Execution(JsonObject report, int index) => report["executions"]![index]!.AsObject();

    private static JsonObject Report()
    {
        string battle = "b8de1951-3be9-4360-85ac-9238406b20c8";
        TelemetryCoverageManifest coverage = new("synthetic-test-v1", [new(1, "test", "synthetic observer", true, true, null)]);
        object[] executions = Enumerable.Range(0, 2).Select(index =>
        {
            long callback = 10 + index * 10;
            long frame = index * 10;
            int hp = 100 + index * 100;
            object State(int health, int stock) => new { id = "party0", current_hp = health, maximum_hp = 500, statuses0 = 0, statuses1 = 0, magic_stocks = new[] { new { magic_id = 21, quantity = stock } } };
            TelemetryOwnerState Owners(long cb, long fr) => new(battle, 1, "memory", "renderer", 1, 0, 0, cb, fr);
            return (object)new
            {
                index = index + 1, completed = true, cleanup_completed = true, tail_presented_frames = 2,
                start_callback = callback, end_callback = callback + 2, start_frame = frame, end_frame = frame + 2, renderer_frames = 2,
                preceding_presentation_ticks = 100 + index * 100,
                start_owners = Owners(callback, frame), end_owners = Owners(callback + 2, frame + 2),
                dispatches = new[] { new { serial = index + 1, callback = callback + 1, actor_id = "party0", ability = "Magic", action_id = 21, requested_effect = 1, executed_effect = 1 } },
                frames = Frames(100 + index * 100, frame, callback),
                traffic = new TelemetryTrafficSnapshot([new(1, TelemetryTrafficKind.CpuCopyPayload, 0, 0, 0)], true, null),
                events = new[] { new { kind = "heal", actor_id = "party0" } },
                resolutions = new[] { new { frame = frame + 1, callback = callback + 1, kind = "heal", actor_id = "party0", original_target_id = "party0", actual_target_id = "party0", resolved_amount = 100, actual_amount = 100, before_hp = hp, after_hp = hp + 100, hp_owner = "character" } },
                before = new[] { State(hp, 10 - index) }, after = new[] { State(hp + 100, 9 - index) }, dropped_events = 0, integrity_error = (string?)null
            };
        }).ToArray();
        return JsonSerializer.SerializeToNode(new
        {
            schema_version = 1, battle_instance_id = battle, expected_effect = 1,
            input = new { actor_id = "party0", ability = "Magic", action_id = 21, draw_cast = false },
            initialization_verified = true, fixture_verified = true, fixture_blockers = Array.Empty<string>(), blocker = (string?)null,
            environment = new { renderer = "synthetic-gpu", width = 1920, height = 1080, gpu = true, real_time = true, active = true },
            audio = new { entry_receipts = 1, dispatch_receipts = 1, enabled = true, audibility_verified = false },
            timestamp_frequency = 1000, coverage, executions
        }, RecordJson.Options)!.AsObject();
    }

    private static TelemetryFrameSummary Frames(long ticks, long frame, long callback, bool slow = false)
    {
        TelemetryFrameCollector collector = new(ticks, 1000, 2);
        long first = ticks + (slow ? 40 : 10);
        collector.RecordPresentation(new(first, frame + 1, "effect", callback + 1, 1, 1, 0));
        collector.RecordPresentation(new(first + 10, frame + 2, "tail", callback + 2, 2, 2, 0));
        return collector.Complete();
    }
}
