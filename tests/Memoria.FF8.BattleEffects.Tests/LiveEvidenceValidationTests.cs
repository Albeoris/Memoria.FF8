using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class LiveEvidenceValidationTests
{
    [Fact]
    public void NativeEjectionProofAllowsStatusDrivenHpRemovalWithoutChangingTheZeroFormula()
    {
        Assert.Equal("PASS", Validate(StatusRemovalReport()).Verdict);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("producer")]
    [InlineData("action_status")]
    [InlineData("applied_status")]
    [InlineData("actor_index")]
    [InlineData("recipient_index")]
    [InlineData("command")]
    [InlineData("action")]
    [InlineData("mask")]
    [InlineData("after_hp")]
    [InlineData("ko")]
    public void NativeEjectionCannotBypassFormulaBoundsWithoutMatchingStatusOwnerDispatchAndHpProof(string forgery)
    {
        JsonObject report = StatusRemovalReport();
        JsonObject execution = Execution(report, 0);
        JsonObject resolution = execution["resolutions"]![0]!.AsObject();
        JsonObject proof = resolution["native_status_removal"]!.AsObject();
        switch (forgery)
        {
            case "missing":
                resolution.Remove("native_status_removal");
                break;
            case "producer":
                proof["producer_address"] = 0x483470;
                break;
            case "action_status":
                proof["action_statuses1"] = 0;
                break;
            case "applied_status":
                proof["applied_battle_statuses"] = 0;
                break;
            case "actor_index":
                proof["actor_index"] = 4;
                break;
            case "recipient_index":
                proof["recipient_index"] = 1;
                break;
            case "command":
                proof["native_command"] = 2;
                break;
            case "action":
                proof["native_action_id"] = 73;
                break;
            case "mask":
                execution["dispatches"]![0]!["native_target_mask"] = 2;
                break;
            case "after_hp":
                resolution["after_hp"] = 1;
                break;
            case "ko":
                execution["after"]![0]!["statuses0"] = 0;
                break;
        }
        LiveEvidenceVerdict verdict = Validate(report);
        Assert.Contains("SEMANTIC_RESOLUTION_INVALID", verdict.Errors);
        if (forgery != "missing")
        {
            Assert.Contains("NATIVE_STATUS_REMOVAL_PROVENANCE_INVALID", verdict.Errors);
        }
    }

    private static JsonObject StatusRemovalReport()
    {
        JsonObject report = Report();
        report["input"] = new JsonObject { ["actor_id"] = "enemy", ["target_id"] = "party0", ["ability"] = "EnemyAbility", ["action_id"] = 72, ["draw_cast"] = false };
        report["expected_effect"] = 174;
        report["expected_native_action"] = 72;
        report["expected_native_command"] = 8;
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            int victim = index;
            long callback = 11 + index * 10;
            JsonObject State(string id, int physical, int hp, bool ko = false) => new() { ["id"] = id, ["native_index"] = physical, ["current_hp"] = hp, ["maximum_hp"] = 500, ["statuses0"] = ko ? 1 : 0, ["statuses1"] = 0, ["magic_stocks"] = new JsonArray() };
            execution["before"] = new JsonArray(State("party0", 0, index == 0 ? 100 : 0, index != 0), State("party1", 1, 200), State("party2", 2, 300), State("enemy", 3, 500));
            execution["after"] = new JsonArray(State("party0", 0, 0, true), State("party1", 1, index == 0 ? 200 : 0, index != 0), State("party2", 2, 300), State("enemy", 3, 500));
            execution["events"] = new JsonArray(new JsonObject { ["kind"] = "ActionComplete", ["actor_id"] = "enemy" });
            execution["dispatches"] = new JsonArray(new JsonObject { ["serial"] = index + 1, ["callback"] = callback, ["actor_id"] = "enemy", ["native_actor_index"] = 3, ["ability"] = "EnemyAbility", ["action_id"] = 72, ["requested_effect"] = 174, ["executed_effect"] = 174, ["native_command"] = 8, ["native_target_mask"] = 1 << victim });
            execution["resolutions"] = new JsonArray(new JsonObject
            {
                ["frame"] = index * 10 + 1,
                ["callback"] = callback,
                ["kind"] = "damage",
                ["actor_id"] = "enemy",
                ["original_target_id"] = $"party{victim}",
                ["actual_target_id"] = $"party{victim}",
                ["resolved_amount"] = 0,
                ["actual_amount"] = victim == 0 ? 100 : 200,
                ["before_hp"] = victim == 0 ? 100 : 200,
                ["after_hp"] = 0,
                ["hp_owner"] = "character",
                ["native_status_removal"] = JsonSerializer.SerializeToNode(new BattleDiagnosticNativeStatusRemoval(0x494410, 3, checked((byte)victim), 8, 72, 0x10000, 0x10000), RecordJson.Options)
            });
        }
        return report;
    }

    [Fact]
    public void Reflect106_second_cast_counts_one_focal_dispatch_and_one_proved_reflected_child()
    {
        LiveEvidenceVerdict result = Validate(ReflectionReport());

        Assert.Equal("PASS", result.Verdict);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.ReflectedChildDispatches);
        Assert.Equal(2, result.Executions.Length);
    }

    [Fact]
    public void Reflected_child_uses_native_magic_semantics_for_a_player_draw_source()
    {
        JsonObject report = ReflectionReport();
        report["input"]!["ability"] = "Draw";
        foreach (JsonNode? execution in report["executions"]!.AsArray())
        {
            execution!["dispatches"]![0]!["ability"] = "Draw";
        }
        Assert.Equal("PASS", Validate(report).Verdict);
        Execution(report, 1)["dispatches"]![1]!["ability"] = "EnemyAbility";
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
    }

    [Fact]
    public void Reflected_child_accepts_original_unavailable_target_only_with_canonical_normalization_and_published_contacts()
    {
        JsonObject report = ReflectionReport(normalized: true);
        JsonNode child = Execution(report, 1)["dispatches"]![1]!;
        Assert.Equal(64, child["reflection"]!["reflected_target_mask"]!.GetValue<int>());
        Assert.Equal(8, child["native_target_mask"]!.GetValue<int>());

        LiveEvidenceVerdict result = Validate(report);
        Assert.Equal("PASS", result.Verdict);
        Assert.Equal(1, result.ReflectedChildDispatches);
    }

    [Theory]
    [InlineData("available")]
    [InlineData("result")]
    [InlineData("contact")]
    [InlineData("generation")]
    [InlineData("producer")]
    [InlineData("missing")]
    public void Reflected_target_normalization_cannot_replace_tainted_canonical_evidence(string corruption)
    {
        JsonObject report = ReflectionReport(normalized: true);
        JsonNode reflection = Execution(report, 1)["dispatches"]![1]!["reflection"]!;
        JsonNode provenance = reflection["target_provenance"]!;
        switch (corruption)
        {
            case "available":
                provenance["resolutions"]![0]!["available_mask"] = 1;
                break;
            case "result":
                provenance["resolutions"]![0]!["result_mask"] = 16;
                break;
            case "contact":
                provenance["contact_recipients"]![0] = 4;
                break;
            case "generation":
                provenance["header_generation"] = 4;
                break;
            case "producer":
                provenance["producer_address"] = 0;
                break;
            case "missing":
                reflection.AsObject().Remove("target_provenance");
                break;
        }
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
    }

    [Theory]
    [InlineData("unlinked")]
    [InlineData("producer")]
    [InlineData("wrong_parent")]
    [InlineData("borrow_previous_execution")]
    [InlineData("source_generation")]
    [InlineData("child_generation")]
    [InlineData("actor")]
    [InlineData("command")]
    [InlineData("action")]
    [InlineData("effect")]
    [InlineData("reflect_status")]
    [InlineData("mask")]
    [InlineData("reused_header")]
    public void Reflected_children_require_unique_same_execution_focal_lineage(string corruption)
    {
        JsonObject report = ReflectionReport();
        JsonObject child = Execution(report, 1)["dispatches"]![1]!.AsObject();
        switch (corruption)
        {
            case "unlinked":
                child.Remove("reflection");
                child["requested_effect"] = 2;
                child["executed_effect"] = 2;
                report["supporting_effects"] = new JsonArray(2);
                break;
            case "producer":
                child["reflection"]!["producer_address"] = 0;
                break;
            case "wrong_parent":
                child["reflection"]!["source_dispatch_serial"] = 99;
                break;
            case "borrow_previous_execution":
                child["reflection"]!["source_dispatch_serial"] = 1;
                break;
            case "source_generation":
                child["reflection"]!["source_header_generation"] = 1;
                break;
            case "child_generation":
                child["native_header_generation"] = 4;
                break;
            case "actor":
                child["actor_id"] = "another-actor";
                break;
            case "command":
                child["native_command"] = 2;
                break;
            case "action":
                child["action_id"] = 32;
                break;
            case "effect":
                child["executed_effect"] = 2;
                break;
            case "reflect_status":
                child["reflection"]!["reflector_statuses1"] = 0;
                break;
            case "mask":
                child["native_target_mask"] = 16;
                break;
            case "reused_header":
                JsonObject duplicate = child.DeepClone().AsObject();
                duplicate["serial"] = 4;
                Execution(report, 1)["dispatches"]!.AsArray().Add(duplicate);
                break;
        }

        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
    }

    [Fact]
    public void Original_command247_requires_explicit_original_invocation_and_no_reflection_receipt()
    {
        JsonObject report = Report();
        report["expected_native_action"] = 21;
        report["expected_native_command"] = 247;
        foreach (JsonNode? execution in report["executions"]!.AsArray())
        {
            execution!["dispatches"]![0]!["native_command"] = 247;
        }
        Assert.Equal("PASS", Validate(report).Verdict);
        report.AsObject().Remove("expected_native_action");
        report.AsObject().Remove("expected_native_command");
        Assert.Contains("DISPATCH_IDENTITY_MISMATCH", Validate(report).Errors);
    }

    private static JsonObject ReflectionReport(bool normalized = false)
    {
        JsonObject report = Report();
        report["expected_effect"] = 106;
        report["expected_native_action"] = 31;
        report["expected_native_command"] = 2;
        report["input"]!["action_id"] = 31;
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            JsonNode source = execution["dispatches"]![0]!;
            source["action_id"] = 31;
            source["native_command"] = 2;
            source["requested_effect"] = 106;
            source["executed_effect"] = 106;
            source["native_actor_index"] = 0;
            source["native_header_index"] = 0;
            source["native_header_generation"] = index + 1;
            source["native_target_mask"] = 1;
            foreach (string boundary in new[] { "before", "after" })
            {
                execution[boundary]![0]!["magic_stocks"]![0]!["magic_id"] = 31;
            }
        }
        Execution(report, 0)["after"]![0]!["statuses1"] = 128;
        Execution(report, 1)["before"]![0]!["statuses1"] = 128;
        Execution(report, 1)["after"]![0]!["statuses1"] = 128;
        JsonObject child = Execution(report, 1)["dispatches"]![0]!.DeepClone().AsObject();
        child["serial"] = 3;
        child["callback"] = 22;
        child["native_command"] = 247;
        child["native_header_index"] = 1;
        child["native_header_generation"] = 3;
        child["native_target_mask"] = 8;
        BattleDiagnosticReflection proof = new(0x487DF0, 2, 0, 2, 1, 3, "party0", 0, "party0", 0, 0x80, "enemy0", 3, 2, 31, 106, 8, 0x10, 31, 106, 0, 8);
        if (normalized)
        {
            proof = proof with
            {
                RecipientId = "enemy6",
                RecipientIndex = 6,
                ReflectedTargetMask = 64,
                TargetProvenance = new(0x48E830, 1, 3, 0, [new(64, 64, 0, 8, false, 8)], [3])
            };
        }
        child["reflection"] = JsonSerializer.SerializeToNode(proof, RecordJson.Options);
        Execution(report, 1)["dispatches"]!.AsArray().Add(child);
        return report;
    }

    [Fact]
    public void Short_effect_metrics_are_unavailable_without_changing_the_original_window_verdict_or_report()
    {
        JsonObject report = Report();
        string original = report.ToJsonString();

        LiveEvidenceVerdict result = Validate(report);

        Assert.Equal("PASS", result.Verdict);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Executions.Length);
        Assert.Equal(4, result.Combined!.FrameCount);
        Assert.Equal(40, result.Combined.DurationMs);
        Assert.Equal(new double[] { 6, 6 }, result.EffectDurationsMs);
        Assert.Equal(2, result.EffectExecutions.Length);
        Assert.All(result.EffectExecutions, effect =>
        {
            Assert.False(effect.Available);
            Assert.Null(effect.MedianFps);
            Assert.Equal(1, effect.BoundaryIntervalCount);
            Assert.Empty(effect.Frames.Intervals);
        });
        Assert.Equal(original, report.ToJsonString());
    }

    [Fact]
    public void Effect_scope_does_not_replace_the_existing_full_window_performance_gate()
    {
        JsonObject report = Report();
        JsonObject first = Execution(report, 0);
        TelemetryFrameSummary original = Frames(100, 0, 10, true);
        first["frames"] = JsonSerializer.SerializeToNode(original, RecordJson.Options);
        first["effect_started_ticks"] = 140;
        first["effect_completed_ticks"] = 150;
        first["effect_duration_ms"] = 10;

        LiveEvidenceVerdict result = Validate(report);

        Assert.Equal("FAIL", result.Verdict);
        Assert.Equal(new[] { "PRESENTATION_BELOW_30_FPS" }, result.Errors);
        Assert.Equal(original.Intervals, result.Executions[0].Intervals);
        Assert.Equal(original.DurationMs, result.Executions[0].DurationMs);
        Assert.Equal(original.MedianFps, result.Executions[0].MedianFps);
        Assert.Equal(100, result.EffectExecutions[0].MedianFps);
        Assert.Equal(1, result.EffectExecutions[0].Frames.FrameCount);
        Assert.Equal(10, result.EffectExecutions[0].Frames.DurationMs);
    }

    [Fact]
    public void Invalid_markers_or_frame_lineage_do_not_publish_an_effect_scope_or_change_existing_errors()
    {
        JsonObject report = Report();
        Execution(report, 0)["effect_duration_ms"] = 20;
        LiveEvidenceVerdict markers = Validate(report);
        Assert.Equal("ERROR", markers.Verdict);
        Assert.Equal(new[] { "EFFECT_TIMING_MARKERS_INVALID" }, markers.Errors);
        Assert.Single(markers.EffectExecutions);

        report = Report();
        Execution(report, 0)["frames"]!["intervals"]![0]!["duration_ms"] = 1;
        LiveEvidenceVerdict lineage = Validate(report);
        Assert.Equal("ERROR", lineage.Verdict);
        Assert.Equal(new[] { "FRAME_LINEAGE_INVALID" }, lineage.Errors);
        Assert.Single(lineage.EffectExecutions);
    }

    [Fact]
    public void Background_presentation_can_pass_with_truthful_inactive_metadata()
    {
        JsonObject report = Report();
        report["environment"]!["active"] = false;
        report["host_status"] = new JsonObject { ["active"] = false, ["update_faulted"] = false, ["render_faulted"] = false, ["error"] = null };

        Assert.Equal("PASS", Validate(report).Verdict);
        Assert.False(report["environment"]!["active"]!.GetValue<bool>());
        Assert.False(report["host_status"]!["active"]!.GetValue<bool>());
    }

    [Fact]
    public void Legacy_inactive_blocker_remains_invalid_even_with_complete_presentations()
    {
        JsonObject report = Report();
        report["environment"]!["active"] = false;
        report["blocker"] = "GRAPHICAL_HOST_INACTIVE";

        LiveEvidenceVerdict verdict = Validate(report);
        Assert.Equal("ERROR", verdict.Verdict);
        Assert.Contains("DRIVER_BLOCKED", verdict.Errors);
    }

    [Fact]
    public void Background_host_still_requires_actual_renderer_frames()
    {
        JsonObject report = Report();
        report["environment"]!["active"] = false;
        Execution(report, 0)["renderer_frames"] = 0;

        LiveEvidenceVerdict verdict = Validate(report);
        Assert.Equal("ERROR", verdict.Verdict);
        Assert.Contains("PRESENTATION_OR_CONTINUITY_UNVERIFIED", verdict.Errors);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slow_interval_is_fail_only_when_all_required_observations_are_valid(bool active)
    {
        JsonObject report = Report();
        report["environment"]!["active"] = active;
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
    public void Resolutions_require_known_actors_and_recipients()
    {
        JsonObject report = Report();
        Execution(report, 0)["resolutions"]![0]!["original_target_id"] = "unrequested";
        Assert.Contains("RESOLUTION_TARGET_UNKNOWN", Validate(report).Errors);
        report = Report();
        Execution(report, 0)["resolutions"]![0]!["actor_id"] = "other";
        Assert.Contains("RESOLUTION_ACTOR_UNKNOWN", Validate(report).Errors);
    }

    [Fact]
    public void Native_resolution_may_select_a_known_recipient_different_from_the_input_hint()
    {
        JsonObject report = Report();
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            JsonNode recipient = execution["before"]![0]!.DeepClone();
            recipient["id"] = "selected-recipient";
            recipient["magic_stocks"] = new JsonArray();
            execution["before"]!.AsArray().Add(recipient.DeepClone());
            JsonNode recipientAfter = recipient.DeepClone();
            recipientAfter["current_hp"] = execution["after"]![0]!["current_hp"]!.DeepClone();
            execution["after"]!.AsArray().Add(recipientAfter);
            execution["before"]![0]!["current_hp"] = 100;
            execution["after"]![0]!["current_hp"] = 100;
            execution["resolutions"]![0]!["original_target_id"] = "selected-recipient";
            execution["resolutions"]![0]!["actual_target_id"] = "selected-recipient";
        }
        LiveEvidenceVerdict result = Validate(report);
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Periodic_status_receipts_reconcile_regen_zombie_and_doom(bool zombie, bool doom)
    {
        Assert.Equal("PASS", Validate(PeriodicStatusReport(zombie, doom)).Verdict);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("producer_address")]
    [InlineData("recipient_id")]
    [InlineData("recipient_index")]
    [InlineData("status_bit")]
    [InlineData("timer_before")]
    [InlineData("decrement")]
    [InlineData("timer_after")]
    [InlineData("saved_statuses")]
    [InlineData("battle_statuses")]
    [InlineData("maximum_hp")]
    [InlineData("resolved_amount")]
    [InlineData("callback")]
    [InlineData("actor")]
    [InlineData("original_target")]
    [InlineData("duplicate_index")]
    [InlineData("missing_timer")]
    public void Periodic_status_provenance_rejects_missing_or_forged_facts(string corruption)
    {
        JsonObject report = PeriodicStatusReport(false, false);
        JsonObject execution = Execution(report, 0);
        JsonObject resolution = execution["resolutions"]![1]!.AsObject();
        JsonObject proof = resolution["periodic_status"]!.AsObject();
        switch (corruption)
        {
            case "missing":
                resolution.Remove("periodic_status");
                break;
            case "actor":
                resolution["actor_id"] = "foreign";
                break;
            case "original_target":
                resolution["original_target_id"] = "party0";
                break;
            case "duplicate_index":
                execution["before"]![0]!["native_index"] = 3;
                break;
            case "missing_timer":
                proof.Remove("timer_before");
                break;
            case "recipient_id":
                proof[corruption] = "party0";
                break;
            default:
                proof[corruption] = corruption == "saved_statuses" ? 0x40 : 1;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData("producer_address")]
    [InlineData("recipient_id")]
    [InlineData("recipient_index")]
    [InlineData("status_bit")]
    [InlineData("timer_before")]
    [InlineData("decrement")]
    [InlineData("timer_after")]
    [InlineData("saved_statuses")]
    [InlineData("battle_statuses")]
    [InlineData("maximum_hp")]
    [InlineData("resolved_amount")]
    [InlineData("callback")]
    public void Every_periodic_status_proof_field_is_required(string field)
    {
        JsonObject report = PeriodicStatusReport(false, false);
        Execution(report, 0)["resolutions"]![1]!["periodic_status"]!.AsObject().Remove(field);
        Assert.Contains(Validate(report).Errors, error => error.StartsWith("REPORT_INVALID:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData((short)100)]
    [InlineData((short)-100)]
    [InlineData((short)0)]
    public void Native_ai_hp_word_receipt_reconciles_the_exact_signed_source_operand(short amount)
    {
        Assert.Equal("PASS", Validate(NativeHpWordReport(amount)).Verdict);
    }

    [Theory]
    [InlineData("producer_address")]
    [InlineData("recipient_id")]
    [InlineData("recipient_index")]
    [InlineData("model_id")]
    [InlineData("script_index")]
    [InlineData("program_counter")]
    [InlineData("script_bytes")]
    [InlineData("opcode")]
    [InlineData("operand")]
    [InlineData("amount")]
    [InlineData("before_hp_word")]
    [InlineData("after_hp_word")]
    [InlineData("callback")]
    public void Every_native_hp_word_proof_field_is_required(string field)
    {
        JsonObject report = NativeHpWordReport();
        Execution(report, 0)["resolutions"]![1]!["native_hp_word"]!.AsObject().Remove(field);
        Assert.Contains(Validate(report).Errors, error => error.StartsWith("REPORT_INVALID:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("producer_address")]
    [InlineData("recipient_id")]
    [InlineData("recipient_index")]
    [InlineData("model_id")]
    [InlineData("script_index")]
    [InlineData("program_counter")]
    [InlineData("opcode")]
    [InlineData("operand")]
    [InlineData("amount")]
    [InlineData("before_hp_word")]
    [InlineData("after_hp_word")]
    [InlineData("callback")]
    [InlineData("script_hash")]
    [InlineData("missing_receipt")]
    [InlineData("actor")]
    [InlineData("delta")]
    [InlineData("source_bytes")]
    [InlineData("duplicate_index")]
    public void Native_hp_word_provenance_rejects_forged_or_missing_facts(string corruption)
    {
        JsonObject report = NativeHpWordReport();
        JsonObject execution = Execution(report, 0);
        JsonObject resolution = execution["resolutions"]![1]!.AsObject();
        JsonObject proof = resolution["native_hp_word"]!.AsObject();
        switch (corruption)
        {
            case "recipient_id":
                proof[corruption] = "party0";
                break;
            case "script_hash":
                execution["before"]![1]!["native_ai_script_sha256"]![3] = new string('0', 64);
                break;
            case "missing_receipt":
                resolution.Remove("native_hp_word");
                break;
            case "actor":
                resolution["actor_id"] = "party0";
                break;
            case "delta":
                resolution["after_hp"] = 301;
                break;
            case "source_bytes":
                proof["script_bytes"] = JsonSerializer.SerializeToNode(System.Collections.Immutable.ImmutableArray.Create<byte>(60, 101, 0, 0), RecordJson.Options);
                break;
            case "duplicate_index":
                execution["before"]![0]!["native_index"] = 3;
                break;
            default:
                proof[corruption] = 2;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData(false, "native_contact")]
    [InlineData(false, "native_hp_word")]
    [InlineData(true, "native_contact")]
    [InlineData(true, "periodic_status")]
    public void Passive_resolution_receipts_reject_mixed_producers(bool nativeWord, string conflictingReceipt)
    {
        JsonObject report = nativeWord ? NativeHpWordReport() : PeriodicStatusReport(false, false);
        JsonObject resolution = Execution(report, 0)["resolutions"]![1]!.AsObject();
        JsonObject otherReport = nativeWord ? PeriodicStatusReport(false, false) : NativeHpWordReport();
        resolution[conflictingReceipt] = conflictingReceipt == "native_contact" ? new JsonObject() : Execution(otherReport, 0)["resolutions"]![1]![conflictingReceipt]!.DeepClone();
        Assert.Contains(nativeWord ? "NATIVE_HP_WORD_PROVENANCE_INVALID" : "PERIODIC_STATUS_PROVENANCE_INVALID", Validate(report).Errors);
    }

    private static JsonObject NativeHpWordReport(short amount = 100)
    {
        JsonObject report = Report();
        ushort operand = unchecked((ushort)amount);
        byte[] script = [60, unchecked((byte)operand), unchecked((byte)(operand >> 8)), 0];
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(script));
        string returnScriptHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 0 }));
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            JsonObject before = execution["before"]![0]!.DeepClone().AsObject();
            before["id"] = "native-hp-recipient";
            before["native_index"] = 3;
            before["model_id"] = 126;
            before["native_ai_script_sha256"] = new JsonArray(returnScriptHash, returnScriptHash, returnScriptHash, hash, returnScriptHash);
            before["current_hp"] = 200;
            JsonObject after = before.DeepClone().AsObject();
            after["current_hp"] = 200 + amount;
            execution["before"]!.AsArray().Add(before);
            execution["after"]!.AsArray().Add(after);
            long callback = execution["start_callback"]!.GetValue<long>() + 1;
            BattleDiagnosticNativeHpWord proof = new(0x487DF0, "native-hp-recipient", 3, 126, 3, 0, System.Collections.Immutable.ImmutableArray.CreateRange(script), 60, operand, amount, 200, unchecked((ushort)(200 + amount)), callback);
            execution["resolutions"]!.AsArray().Add(new JsonObject
            {
                ["frame"] = index * 10 + 1, ["callback"] = callback, ["kind"] = "native_ai_hp_word", ["actor_id"] = null,
                ["original_target_id"] = "native-hp-recipient", ["actual_target_id"] = "native-hp-recipient", ["resolved_amount"] = amount, ["actual_amount"] = amount,
                ["before_hp"] = 200, ["after_hp"] = 200 + amount, ["hp_owner"] = "enemy", ["native_hp_word"] = JsonSerializer.SerializeToNode(proof, RecordJson.Options)
            });
        }
        return report;
    }

    private static JsonObject PeriodicStatusReport(bool zombie, bool doom)
    {
        JsonObject report = Report();
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            JsonObject before = execution["before"]![0]!.DeepClone().AsObject();
            before["id"] = "periodic-recipient";
            before["native_index"] = 3;
            before["current_hp"] = 200;
            before["statuses0"] = zombie ? 0x40 : 0;
            before["statuses1"] = doom ? 0x400 : 0x10;
            JsonObject after = before.DeepClone().AsObject();
            int amount = doom ? 200 : 25;
            after["current_hp"] = doom ? 0 : zombie ? 175 : 225;
            execution["before"]!.AsArray().Add(before);
            execution["after"]!.AsArray().Add(after);
            long callback = execution["start_callback"]!.GetValue<long>() + 1;
            BattleDiagnosticPeriodicStatus proof = new(0x483470, "periodic-recipient", 3, doom ? 10 : 4, doom ? (short)0 : (short)60, doom ? 0 : 2, doom ? (short)-1111 : (short)58, zombie ? (ushort)0x40 : (ushort)0, doom ? 0x400u : 0x10u, 500, amount, callback);
            JsonObject resolution = new()
            {
                ["frame"] = index * 10 + 1, ["callback"] = callback, ["kind"] = zombie || doom ? "damage" : "healing", ["actor_id"] = null,
                ["original_target_id"] = "periodic-recipient", ["actual_target_id"] = "periodic-recipient", ["resolved_amount"] = amount, ["actual_amount"] = amount,
                ["before_hp"] = 200, ["after_hp"] = after["current_hp"]!.DeepClone(), ["hp_owner"] = "enemy", ["periodic_status"] = JsonSerializer.SerializeToNode(proof, RecordJson.Options)
            };
            execution["resolutions"]!.AsArray().Add(resolution);
        }
        return report;
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

    [Fact]
    public void Guardian_contact_provenance_is_optional_but_validated_when_present()
    {
        JsonObject report = GuardianContactReport();
        Assert.Equal("PASS", Validate(report).Verdict);
        Execution(report, 1)["resolutions"]![0]!.AsObject().Remove("native_contact");
        Assert.Equal("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData("producer")]
    [InlineData("serial")]
    [InlineData("submitted_target")]
    [InlineData("actor")]
    [InlineData("recipient")]
    [InlineData("header_index")]
    [InlineData("unknown_header")]
    [InlineData("unknown_pointer")]
    [InlineData("contact_pointer")]
    [InlineData("contact_arena")]
    [InlineData("missing_mask")]
    [InlineData("command")]
    [InlineData("action")]
    [InlineData("header_actor")]
    [InlineData("contact_index")]
    [InlineData("contact_recipient")]
    [InlineData("reallocated")]
    [InlineData("duplicate_index")]
    [InlineData("dispatch_command")]
    [InlineData("dispatch_after_resolution")]
    public void Guardian_contact_provenance_cannot_replace_missing_or_conflicting_identity(string corruption)
    {
        JsonObject report = GuardianContactReport();
        JsonObject execution = Execution(report, 1);
        JsonObject resolution = execution["resolutions"]![0]!.AsObject();
        JsonObject proof = resolution["native_contact"]!.AsObject();
        switch (corruption)
        {
            case "producer":
                proof["producer_address"] = 1;
                break;
            case "serial":
                proof["dispatch_serial"] = 1;
                break;
            case "submitted_target":
                proof["submitted_target_id"] = "recipient";
                break;
            case "actor":
                proof["actor_id"] = "recipient";
                break;
            case "recipient":
                proof["recipient_id"] = "party0";
                break;
            case "header_index":
                proof["header_index"] = 1;
                break;
            case "unknown_header":
                proof["header_unknown_byte_mask"] = 1 << 4;
                break;
            case "unknown_pointer":
                proof["header_unknown_byte_mask"] = 1 << 8;
                break;
            case "contact_pointer":
                proof["header_payload"]![8] = 0;
                break;
            case "contact_arena":
                proof["header_payload"]![16] = 50;
                break;
            case "missing_mask":
                proof.Remove("header_unknown_byte_mask");
                Assert.Contains(Validate(report).Errors, error => error.StartsWith("REPORT_INVALID:", StringComparison.Ordinal));
                return;
            case "command":
                proof["header_payload"]![1] = 253;
                break;
            case "action":
                proof["header_payload"]![4] = 77;
                break;
            case "header_actor":
                proof["header_payload"]![0] = 1;
                break;
            case "contact_index":
                proof["retained_contact_index"] = 1;
                break;
            case "contact_recipient":
                proof["contact_payload"]![0] = 5;
                break;
            case "reallocated":
                execution["after"]![1]!["native_index"] = 5;
                break;
            case "duplicate_index":
                execution["before"]![0]!["native_index"] = 4;
                break;
            case "dispatch_command":
                execution["dispatches"]![0]!["native_command"] = 253;
                break;
            case "dispatch_after_resolution":
                execution["resolutions"]![0]!["callback"] = 20;
                break;
        }
        Assert.Contains("NATIVE_CONTACT_PROVENANCE_INVALID", Validate(report).Errors);
    }

    private static JsonObject GuardianContactReport()
    {
        JsonObject report = Report();
        report["input"]!["ability"] = "GuardianForce";
        report["input"]!["action_id"] = 14;
        report["expected_native_action"] = 78;
        report["expected_native_command"] = 254;
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            JsonObject dispatch = execution["dispatches"]![0]!.AsObject();
            dispatch["ability"] = "GuardianForce";
            dispatch["action_id"] = 78;
            dispatch["native_command"] = 254;
            JsonNode recipientBefore = execution["before"]![0]!.DeepClone();
            JsonNode recipientAfter = execution["after"]![0]!.DeepClone();
            recipientBefore["id"] = "recipient";
            recipientAfter["id"] = "recipient";
            recipientBefore["native_index"] = 4;
            recipientAfter["native_index"] = 4;
            execution["before"]![0]!["native_index"] = 0;
            execution["after"]![0]!["native_index"] = 0;
            execution["after"]![0]!["current_hp"] = execution["before"]![0]!["current_hp"]!.DeepClone();
            execution["before"]!.AsArray().Add(recipientBefore);
            execution["after"]!.AsArray().Add(recipientAfter);
            JsonObject resolution = execution["resolutions"]![0]!.AsObject();
            resolution["original_target_id"] = "recipient";
            resolution["actual_target_id"] = "recipient";
            byte[] header = new byte[20];
            header[1] = 254;
            header[4] = 78;
            header[16] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), NativeBattleActionPacketsState.ContactBase);
            NativeBattleContactPacket packet = new(new(4, 0, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0));
            BattleDiagnosticNativeContact proof = new(0x4850A0, index + 1, "party0", "party0", 0, "recipient", 4, 0, [.. header], 0, 0, 0, [.. packet.Encode()]);
            resolution["native_contact"] = JsonSerializer.SerializeToNode(proof, RecordJson.Options);
        }
        return report;
    }

    private static LiveEvidenceVerdict Validate(JsonObject report)
    {
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return LiveEvidenceValidation.Validate(document.RootElement);
    }

    private static JsonObject Execution(JsonObject report, int index) => report["executions"]![index]!.AsObject();

    [Fact]
    public void Explicit_terminal_hold_receipts_preserve_two_executions_and_declared_repair_lineage()
    {
        LiveEvidenceVerdict result = Validate(TerminalHoldReport());
        Assert.True(result.Verdict == "PASS", string.Join(Environment.NewLine, result.Errors));
        Assert.Contains("CONTROLLED_DEFEAT_HOLD_REQUIRES_SEPARATE_NATURAL_TERMINAL_CHECK", result.DiagnosticIssues);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Terminal_hold_requires_observed_incapacitation_after_each_execution(int index)
    {
        JsonObject report = TerminalHoldReport();
        Execution(report, index)["after"]![0]!["statuses0"] = 0;
        Assert.Contains("TERMINAL_HOLD_DEFEAT_NOT_OBSERVED", Validate(report).Errors);
    }

    [Theory]
    [InlineData("undeclared")]
    [InlineData("missing")]
    [InlineData("kind")]
    [InlineData("sequence")]
    [InlineData("hold_id")]
    [InlineData("lifecycle_id")]
    [InlineData("native_result")]
    [InlineData("callback")]
    [InlineData("frame")]
    [InlineData("drained")]
    [InlineData("reset_count")]
    [InlineData("party")]
    [InlineData("rearm_state")]
    [InlineData("random")]
    public void Terminal_hold_rejects_undeclared_or_forged_owner_timing_and_rearm(string corruption)
    {
        JsonObject report = TerminalHoldReport();
        JsonObject receipt = report["terminal_holds"]![1]!.AsObject();
        switch (corruption)
        {
            case "undeclared":
                report.Remove("declared_terminal_hold");
                break;
            case "missing":
                report.Remove("terminal_holds");
                break;
            case "kind":
                receipt["kind"] = "released";
                break;
            case "hold_id":
            case "lifecycle_id":
                receipt[corruption] = Guid.NewGuid().ToString();
                break;
            case "party":
                receipt["party"]![0]!["native_index"] = 3;
                break;
            case "rearm_state":
                receipt["party"]![0]!["current_hp"] = 99;
                break;
            case "drained":
                receipt[corruption] = false;
                break;
            case "random":
                receipt["random"]!["battle"]!["lane"] = 1;
                break;
            default:
                receipt[corruption] = 2;
                break;
        }
        Assert.NotEqual("PASS", Validate(report).Verdict);
    }

    [Theory]
    [InlineData("hold_id")]
    [InlineData("lifecycle_id")]
    [InlineData("kind")]
    [InlineData("sequence")]
    [InlineData("frame")]
    [InlineData("callback")]
    [InlineData("candidate")]
    [InlineData("native_result")]
    [InlineData("drained")]
    [InlineData("restore_count")]
    [InlineData("reset_count")]
    [InlineData("random")]
    [InlineData("party")]
    public void Every_terminal_hold_receipt_fact_is_required(string field)
    {
        JsonObject report = TerminalHoldReport();
        report["terminal_holds"]![1]!.AsObject().Remove(field);
        Assert.Contains(Validate(report).Errors, error => error.StartsWith("REPORT_INVALID:", StringComparison.Ordinal));
    }

    private static JsonObject TerminalHoldReport()
    {
        JsonObject report = ReportWithRepair();
        string lifecycle = "78123682-5974-42bd-84b5-36abb7df6a18";
        string hold = "77629692-d5c1-49c2-848d-8501e381b4fa";
        for (int index = 0; index < 2; index++)
        {
            JsonObject execution = Execution(report, index);
            execution["before"]![0]!["current_hp"] = 100;
            execution["before"]![0]!["statuses0"] = 0;
            execution["after"]![0]!["current_hp"] = 0;
            execution["after"]![0]!["statuses0"] = 1;
            foreach (string boundary in new[] { "before", "after" })
            {
                execution[boundary]![0]!["maximum_hp"] = 100;
                execution[boundary]![0]!["native_index"] = 0;
                execution[boundary]![0]!["model_id"] = null;
                execution[boundary]![0]!["native_ai_script_sha256"] = null;
            }
            execution["start_owners"]!["retained_memory_owner_id"] = lifecycle;
            execution["end_owners"]!["retained_memory_owner_id"] = lifecycle;
            execution["resolutions"]![0]!["kind"] = "damage";
            execution["resolutions"]![0]!["before_hp"] = 100;
            execution["resolutions"]![0]!["after_hp"] = 0;
        }
        JsonObject repair = report["repairs"]![0]!.AsObject();
        repair["lifecycle_id"] = lifecycle;
        report["declared_repairs"]![0]!["revive"] = true;
        repair["changes"]![0]!["request"]!["revive"] = true;
        repair["changes"]![0]!["before"] = Execution(report, 0)["after"]![0]!.DeepClone();
        repair["changes"]![0]!["after"] = Execution(report, 1)["before"]![0]!.DeepClone();
        report["declared_terminal_hold"] = JsonSerializer.SerializeToNode(new BattleDiagnosticTerminalHoldDeclaration("Controlled defeat repetition", ["party0"], 100), RecordJson.Options);
        JsonArray receipts = [];
        string[] kinds = ["held", "rearmed", "held", "released"];
        int[] sequences = [1, 1, 2, 2];
        long[] callbacks = [11, 16, 21, 22];
        long[] frames = [1, 5, 11, 12];
        for (int index = 0; index < 4; index++)
        {
            JsonNode party = index == 1 ? Execution(report, 1)["before"]![0]! : Execution(report, index < 2 ? 0 : 1)["after"]![0]!;
            receipts.Add(new JsonObject
            {
                ["hold_id"] = hold, ["lifecycle_id"] = lifecycle, ["kind"] = kinds[index], ["sequence"] = sequences[index], ["frame"] = frames[index],
                ["callback"] = callbacks[index], ["candidate"] = "Defeat", ["native_result"] = null, ["drained"] = index is 1 or 3,
                ["restore_count"] = 0, ["reset_count"] = 0, ["random"] = repair["random_after"]!.DeepClone(), ["party"] = new JsonArray(party.DeepClone())
            });
        }
        report["terminal_holds"] = receipts;
        return report;
    }

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
