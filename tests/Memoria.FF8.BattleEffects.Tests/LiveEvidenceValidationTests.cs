using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class LiveEvidenceValidationTests
{
    [Fact]
    public void Native_dispatch_identity_can_differ_from_the_semantic_guardian_force_selection()
    {
        JsonObject report = Report();
        report["input"]!["ability"] = "GuardianForce";
        report["input"]!["action_id"] = 0;
        report["expected_native_action"] = 64;
        report["expected_native_command"] = 254;
        foreach (JsonNode? execution in report["executions"]!.AsArray())
        {
            execution!["dispatches"]![0]!["ability"] = "GuardianForce";
            execution["dispatches"]![0]!["action_id"] = 64;
            execution["dispatches"]![0]!["native_command"] = 254;
        }

        Assert.Equal("PASS", Validate(report).Verdict);
        Execution(report, 1)["dispatches"]![0]!["action_id"] = 0;
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
        Execution(report, 1)["dispatches"]![0]!["action_id"] = 64;
        Execution(report, 1)["dispatches"]![0]!["native_command"] = 4;
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
        report.Remove("expected_native_command");
        Assert.Contains("NATIVE_INVOCATION_IDENTITY_INVALID", Validate(report).Errors);
    }

    [Fact]
    public void Supporting_dispatches_require_explicit_whitelisting_and_unchanged_actual_effect_identity()
    {
        JsonObject report = Report();
        report["supporting_effects"] = new JsonArray(2);
        for (int index = 0; index < 2; index++)
        {
            JsonArray dispatches = Execution(report, index)["dispatches"]!.AsArray();
            dispatches[0]!["serial"] = index * 2 + 1;
            JsonObject supporting = dispatches[0]!.DeepClone().AsObject();
            supporting["serial"] = index * 2 + 2;
            supporting["action_id"] = 65535;
            supporting["requested_effect"] = 2;
            supporting["executed_effect"] = 2;
            dispatches.Add(supporting);
        }

        Assert.Equal("PASS", Validate(report).Verdict);
        report["supporting_effects"] = new JsonArray();
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
        report["supporting_effects"] = new JsonArray(2);
        Execution(report, 1)["dispatches"]![1]!["executed_effect"] = 3;
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
        Execution(report, 1)["dispatches"]![1]!["executed_effect"] = 2;
        Execution(report, 1)["dispatches"]![1]!["actor_id"] = "another-actor";
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
    }

    [Fact]
    public void A_declared_between_execution_hp_repair_preserves_the_retained_owners()
    {
        Assert.Equal("PASS", Validate(ReportWithRepair()).Verdict);
    }

    [Fact]
    public void Repair_native_simulation_frames_are_distinct_from_successful_presentation_indexes()
    {
        JsonObject report = ReportWithRepair();
        report["repairs"]![0]!["frame"] = 135;
        Assert.Equal("PASS", Validate(report).Verdict);
    }

    [Fact]
    public void Repair_status_validation_preserves_unremoved_bits_and_recomputes_native_low_hp_flags()
    {
        JsonObject report = ReportWithRepair();
        JsonObject receipt = report["repairs"]![0]!.AsObject();
        JsonObject change = receipt["changes"]![0]!.AsObject();
        foreach (JsonNode state in new[] { Execution(report, 0)["after"]![0]!, change["before"]! })
        {
            state["statuses0"] = 0x20A;
            state["statuses1"] = 0x42;
        }
        foreach (JsonNode state in new[] { Execution(report, 1)["before"]![0]!, change["after"]! })
        {
            state["statuses0"] = 0x308;
            state["statuses1"] = 2;
        }
        foreach (JsonNode request in new[] { report["declared_repairs"]![0]!, change["request"]! })
        {
            request["remove_statuses0"] = 2;
            request["remove_statuses1"] = 0x40;
        }

        Assert.Equal("PASS", Validate(report).Verdict);
        Execution(report, 1)["before"]![0]!["statuses0"] = 0x300;
        change["after"]!["statuses0"] = 0x300;
        Assert.Contains("REPAIR_STATUS_MUTATION_INVALID", Validate(report).Errors);
    }

    [Theory]
    [InlineData("detached_before", "REPAIR_STATE_LINEAGE_INVALID")]
    [InlineData("detached_after", "REPAIR_STATE_LINEAGE_INVALID")]
    [InlineData("added_status", "REPAIR_STATUS_MUTATION_INVALID")]
    [InlineData("incorrect_hp_flags", "REPAIR_STATUS_MUTATION_INVALID")]
    [InlineData("duplicate_actor", "REPAIR_DECLARATION_MISMATCH")]
    [InlineData("missing_receipt", "REPAIR_EVIDENCE_INVALID")]
    public void Repair_receipts_must_anchor_the_execution_states_and_only_apply_declared_status_changes(string forgery, string expectedError)
    {
        JsonObject report = ReportWithRepair();
        JsonObject receipt = report["repairs"]![0]!.AsObject();
        JsonObject change = receipt["changes"]![0]!.AsObject();
        switch (forgery)
        {
            case "detached_before":
                change["before"]!["current_hp"] = 199;
                break;
            case "detached_after":
                Execution(report, 1)["before"]![0]!["statuses1"] = 4;
                break;
            case "added_status":
                change["after"]!["statuses1"] = 4;
                Execution(report, 1)["before"]![0]!["statuses1"] = 4;
                break;
            case "incorrect_hp_flags":
                change["after"]!["statuses0"] = 0;
                Execution(report, 1)["before"]![0]!["statuses0"] = 0;
                break;
            case "duplicate_actor":
                report["declared_repairs"]!.AsArray().Add(report["declared_repairs"]![0]!.DeepClone());
                receipt["changes"]!.AsArray().Add(change.DeepClone());
                break;
            case "missing_receipt":
                report["repairs"] = new JsonArray();
                break;
        }

        Assert.Contains(expectedError, Validate(report).Errors);
    }

    [Theory]
    [InlineData("random", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("items", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("callback_during_first", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("callback_during_second", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("callback_advanced", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    [InlineData("request", "REPAIR_DECLARATION_MISMATCH")]
    [InlineData("restore", "REPAIR_OWNER_CONTINUITY_BROKEN")]
    public void Forged_repair_receipts_cannot_hide_rng_changes_or_undeclared_mutations(string forgery, string expectedError)
    {
        JsonObject report = ReportWithRepair();
        JsonObject receipt = report["repairs"]![0]!.AsObject();
        switch (forgery)
        {
            case "random":
                receipt["random_after"]!["battle"]!["lane"] = 1;
                break;
            case "items":
                receipt["items_after"] = new JsonArray(new JsonObject { ["item_id"] = 1, ["quantity"] = 1 });
                break;
            case "callback_during_first":
                receipt["callback_before"] = 11;
                receipt["callback_after"] = 11;
                break;
            case "callback_during_second":
                receipt["callback_before"] = 21;
                receipt["callback_after"] = 21;
                break;
            case "callback_advanced":
                receipt["callback_after"] = 17;
                break;
            case "request":
                receipt["changes"]![0]!["request"]!["hit_points"] = 200;
                break;
            case "restore":
                receipt["restore_count"] = 1;
                break;
        }

        Assert.Contains(expectedError, Validate(report).Errors);
    }

    [Fact]
    public void Any_effect_can_pass_smoke_without_internal_copy_instrumentation()
    {
        JsonObject report = Report();
        report["expected_effect"] = 77;
        report.Remove("coverage");
        foreach (JsonNode? entry in report["executions"]!.AsArray())
        {
            entry!.AsObject().Remove("traffic");
            entry["dispatches"]![0]!["requested_effect"] = 77;
            entry["dispatches"]![0]!["executed_effect"] = 77;
        }
        LiveEvidenceVerdict result = Validate(report);
        Assert.Equal("PASS", result.Verdict);
        Assert.Contains("COPY_TRAFFIC_NOT_MEASURED", result.DiagnosticIssues);
        Assert.Contains("not measured", result.CopyTrafficScope);
        Execution(report, 1)["dispatches"] = new JsonArray();
        Assert.Contains("DISPATCH_COUNT_INVALID", Validate(report).Errors);
    }

    [Fact]
    public void Non_hp_actions_require_normal_action_completion_without_fabricated_damage_contacts()
    {
        JsonObject report = Report();
        foreach (JsonNode? entry in report["executions"]!.AsArray())
        {
            entry!["resolutions"] = new JsonArray();
            entry["after"]![0]!["current_hp"] = entry["before"]![0]!["current_hp"]!.DeepClone();
            entry["events"] = new JsonArray(new JsonObject { ["kind"] = "ActionComplete", ["actor_id"] = "party0" });
        }
        Assert.Equal("PASS", Validate(report).Verdict);
        Execution(report, 0)["events"] = new JsonArray();
        Assert.Contains("SEMANTIC_RESOLUTION_UNVERIFIED", Validate(report).Errors);
    }

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
    public void Reconstructed_owner_and_muted_route_are_errors_but_optional_copy_coverage_is_separate()
    {
        JsonObject report = Report();
        Execution(report, 1)["start_owners"]!["renderer_owner_id"] = "replacement";
        Assert.Contains("CONTINUITY_BROKEN", Validate(report).Errors);
        report = Report();
        report["coverage"] = JsonSerializer.SerializeToNode(TelemetryCoverageManifest.PreparationOnly, RecordJson.Options);
        Assert.Equal("PASS", Validate(report).Verdict);
        Assert.Contains("COPY_TRAFFIC_UNAVAILABLE", Validate(report).DiagnosticIssues);
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
        Assert.Contains("LARGE_COPY_REVIEW_REQUIRED", Validate(report).DiagnosticIssues);
        Assert.Equal("PASS", Validate(report).Verdict);
        counters.Remove(copy);
        LiveEvidenceVerdict missing = Validate(report);
        Assert.Equal("PASS", missing.Verdict);
        Assert.Contains("COPY_TRAFFIC_INVALID_OR_INCOMPLETE", missing.DiagnosticIssues);
        Execution(report, 0)["traffic"]!["sites"] = new JsonArray(counters.Where(node => node!["kind"]!.GetValue<int>() == (int)TelemetryTrafficKind.Fill).Select(node => node!.DeepClone()).ToArray());
        Assert.Contains("COPY_TRAFFIC_INVALID_OR_INCOMPLETE", Validate(report).DiagnosticIssues);
    }

    [Fact]
    public void Coverage_must_match_the_authoritative_version_and_exact_selected_effect_sites()
    {
        JsonObject report = Report();
        report["coverage"]!["instrumentation_version"] = "invented";
        Assert.Contains("COPY_TELEMETRY_MANIFEST_MISMATCH", Validate(report).DiagnosticIssues);
        report = Report();
        report["coverage"]!["sites"]!.AsArray().RemoveAt(0);
        Assert.Contains("COPY_TELEMETRY_MANIFEST_MISMATCH", Validate(report).DiagnosticIssues);
        report = Report();
        report["coverage"]!["sites"]![0]!["api"] = "omitted native owner";
        Assert.Contains("COPY_TELEMETRY_MANIFEST_MISMATCH", Validate(report).DiagnosticIssues);
    }

    [Fact]
    public void Focal_resolutions_require_the_submitted_actor_and_original_recipient()
    {
        JsonObject report = Report();
        Execution(report, 0)["resolutions"]![0]!["original_target_id"] = "unrequested";
        Assert.Contains("FOCAL_RESOLUTION_TARGET_MISMATCH", Validate(report).Errors);
        report = Report();
        Execution(report, 0)["resolutions"]![0]!["actor_id"] = "other";
        Assert.Contains("RESOLUTION_ACTOR_UNKNOWN", Validate(report).Errors);
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

    private static JsonObject ReportWithRepair()
    {
        JsonObject report = Report();
        JsonObject second = Execution(report, 1);
        second["before"]![0]!["current_hp"] = 100;
        second["before"]![0]!["statuses0"] = 0x300;
        second["after"]![0]!["current_hp"] = 200;
        second["after"]![0]!["statuses0"] = 0x200;
        second["resolutions"]![0]!["before_hp"] = 100;
        second["resolutions"]![0]!["after_hp"] = 200;
        JsonObject request = new() { ["actor_id"] = "party0", ["hit_points"] = 100, ["revive"] = false, ["remove_statuses0"] = 0, ["remove_statuses1"] = 0 };
        report["declared_repairs"] = new JsonArray(request.DeepClone());
        JsonObject random = new() { ["battle"] = new JsonObject { ["indexes"] = new JsonArray(0, 1, 2, 3, 4, 5, 6, 7), ["lane"] = 0 }, ["crt"] = null, ["ui"] = null, ["global"] = null };
        report["repairs"] = new JsonArray(new JsonObject
        {
            ["lifecycle_id"] = "memory", ["frame"] = 5, ["callback_before"] = 16, ["callback_after"] = 16,
            ["random_before"] = random.DeepClone(), ["random_after"] = random.DeepClone(), ["restore_count"] = 0, ["reset_count"] = 0,
            ["items_before"] = new JsonArray(), ["items_after"] = new JsonArray(),
            ["changes"] = new JsonArray(new JsonObject { ["request"] = request, ["before"] = Execution(report, 0)["after"]![0]!.DeepClone(), ["after"] = second["before"]![0]!.DeepClone() })
        });
        return report;
    }

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
                events = new[] { new { kind = "heal", actor_id = "party0" }, new { kind = "ActionComplete", actor_id = "party0" } },
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
