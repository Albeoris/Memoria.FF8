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
        Assert.Equal(new double[] { 6, 6 }, result.EffectDurationsMs);
        Assert.Equal(40, result.Executions.Sum(execution => execution.DurationMs));
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

    [Fact]
    public void Effect_duration_uses_actual_invoke_to_drain_markers_and_cannot_include_admission_or_tail()
    {
        JsonObject report = Report();
        Execution(report, 0)["effect_duration_ms"] = 20;
        Assert.Contains("EFFECT_TIMING_MARKERS_INVALID", Validate(report).Errors);
        report = Report();
        Execution(report, 0)["effect_completed_ticks"] = 121;
        Assert.Contains("EFFECT_TIMING_MARKERS_INVALID", Validate(report).Errors);
        report = Report();
        Execution(report, 0).Remove("effect_started_ticks");
        Assert.Equal("ERROR", Validate(report).Verdict);
    }

    [Fact]
    public void Removing_copy_counters_cannot_replace_a_complete_table_with_zero_fill_claims()
    {
        JsonObject report = Report();
        JsonArray counters = Execution(report, 0)["traffic"]!["sites"]!.AsArray();
        JsonNode copy = counters.First(node => node!["site_id"]!.GetValue<int>() == 1002 && node["kind"]!.GetValue<int>() == (int)TelemetryTrafficKind.CpuCopyPayload)!;
        copy["bytes"] = 1024L * 1024 * 1024;
        copy["operations"] = 1;
        copy["largest_operation_bytes"] = 1024L * 1024 * 1024;
        Assert.Contains("LARGE_COPY_REVIEW_REQUIRED", Validate(report).Errors);
        counters.Remove(copy);
        LiveEvidenceVerdict missing = Validate(report);
        Assert.Equal("ERROR", missing.Verdict);
        Assert.Contains("TRAFFIC_UNAVAILABLE_OR_INVALID", missing.Errors);
        Execution(report, 0)["traffic"]!["sites"] = new JsonArray(counters.Where(node => node!["kind"]!.GetValue<int>() == (int)TelemetryTrafficKind.Fill).Select(node => node!.DeepClone()).ToArray());
        Assert.Contains("TRAFFIC_UNAVAILABLE_OR_INVALID", Validate(report).Errors);
    }

    [Fact]
    public void Coverage_must_match_the_authoritative_version_and_exact_selected_effect_sites()
    {
        JsonObject report = Report();
        report["coverage"]!["instrumentation_version"] = "invented";
        Assert.Contains("TELEMETRY_MANIFEST_MISMATCH", Validate(report).Errors);
        report = Report();
        report["coverage"]!["sites"]!.AsArray().RemoveAt(0);
        Assert.Contains("TELEMETRY_MANIFEST_MISMATCH", Validate(report).Errors);
        report = Report();
        report["coverage"]!["sites"]![0]!["api"] = "omitted native owner";
        Assert.Contains("TELEMETRY_MANIFEST_MISMATCH", Validate(report).Errors);
    }

    [Fact]
    public void Focal_resolutions_require_the_submitted_actor_and_original_recipient()
    {
        JsonObject report = Report();
        Execution(report, 0)["resolutions"]![0]!["original_target_id"] = "unrequested";
        Assert.Contains("FOCAL_RESOLUTION_TARGET_MISMATCH", Validate(report).Errors);
        report = Report();
        Execution(report, 0)["resolutions"]![0]!["actor_id"] = "other";
        Assert.Contains("SEMANTIC_RESOLUTION_UNVERIFIED", Validate(report).Errors);
    }

    [Fact]
    public void Reflection_preserves_the_original_recipient_and_reconciles_the_separate_actual_recipient()
    {
        JsonObject report = Report();
        JsonObject execution = Execution(report, 0);
        JsonNode recipientBefore = execution["before"]![0]!.DeepClone();
        JsonNode recipientAfter = execution["after"]![0]!.DeepClone();
        recipientBefore["id"] = "reflected-recipient";
        recipientAfter["id"] = "reflected-recipient";
        recipientBefore["magic_stocks"] = new JsonArray();
        recipientAfter["magic_stocks"] = new JsonArray();
        execution["before"]!.AsArray().Add(recipientBefore);
        execution["after"]!.AsArray().Add(recipientAfter);
        execution["after"]![0]!["current_hp"] = 100;
        execution["resolutions"]![0]!["actual_target_id"] = "reflected-recipient";
        Assert.Equal("PASS", Validate(report).Verdict);
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
        TelemetryCoverageManifest coverage = TelemetryCoverageManifest.ManagedPayload(1);
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
                effect_started_ticks = 102 + index * 100, effect_completed_ticks = 108 + index * 100, effect_duration_ms = 6,
                start_owners = Owners(callback, frame), end_owners = Owners(callback + 2, frame + 2),
                dispatches = new[] { new { serial = index + 1, callback = callback + 1, actor_id = "party0", ability = "Magic", action_id = 21, requested_effect = 1, executed_effect = 1 } },
                frames = Frames(100 + index * 100, frame, callback),
                traffic = new TelemetryTrafficCollector(coverage).Complete(),
                events = new[] { new { kind = "heal", actor_id = "party0" } },
                resolutions = new[] { new { frame = frame + 1, callback = callback + 1, kind = "heal", actor_id = "party0", original_target_id = "party0", actual_target_id = "party0", resolved_amount = 100, actual_amount = 100, before_hp = hp, after_hp = hp + 100, hp_owner = "character" } },
                before = new[] { State(hp, 10 - index) }, after = new[] { State(hp + 100, 9 - index) }, dropped_events = 0, integrity_error = (string?)null
            };
        }).ToArray();
        return JsonSerializer.SerializeToNode(new
        {
            schema_version = 1, battle_instance_id = battle, expected_effect = 1,
            input = new { actor_id = "party0", target_id = "party0", ability = "Magic", action_id = 21, draw_cast = false },
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
