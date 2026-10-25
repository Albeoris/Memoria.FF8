using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.BattleEffects;

public sealed record LiveEvidenceVerdict(string Verdict, string[] Errors, TelemetryFrameSummary[] Executions, double[] EffectDurationsMs, TelemetryFrameSummary? Combined, bool CampaignAcceptance, string AudioScope, string[] DiagnosticIssues, string CopyTrafficScope);

/// <summary>Independently checks retained live observations. Campaign fixture provenance and reviewer acceptance remain distinct from telemetry admission.</summary>
public static class LiveEvidenceValidation
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    public static LiveEvidenceVerdict ValidateFile(string path, CaseRecipe? recipe = null)
    {
        FileInfo file = new(path);
        if (file.Length is <= 0 or > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Live telemetry report is empty or exceeds its bounded size.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return Validate(document.RootElement, recipe);
    }

    public static LiveEvidenceVerdict Validate(JsonElement report, CaseRecipe? recipe = null)
    {
        List<string> errors = [];
        List<string> diagnostics = [];
        List<TelemetryFrameSummary> frames = [];
        List<double> effectDurations = [];
        try
        {
            RejectDuplicateProperties(report);
            if (Number(report, "schema_version") != 1 || !Guid.TryParse(Text(report, "battle_instance_id"), out Guid identity) || identity == Guid.Empty)
            {
                errors.Add("REPORT_IDENTITY_INVALID");
            }
            if (!Flag(report, "initialization_verified"))
            {
                errors.Add("INITIALIZATION_UNVERIFIED");
            }
            if (!Flag(report, "fixture_verified") || report.GetProperty("fixture_blockers").GetArrayLength() != 0)
            {
                errors.Add("FIXTURE_UNVERIFIED");
            }
            if (report.GetProperty("blocker").ValueKind != JsonValueKind.Null)
            {
                errors.Add("DRIVER_BLOCKED");
            }
            JsonElement environment = report.GetProperty("environment");
            if (!Flag(environment, "gpu") || !Flag(environment, "real_time") || !Flag(environment, "active") || Number(environment, "width") != 1920 || Number(environment, "height") != 1080 || string.IsNullOrWhiteSpace(Text(environment, "renderer")))
            {
                errors.Add("REFERENCE_ENVIRONMENT_UNVERIFIED");
            }
            JsonElement audio = report.GetProperty("audio");
            if (!Flag(audio, "enabled") || Number(audio, "entry_receipts") != 1 || Number(audio, "dispatch_receipts") < 0)
            {
                errors.Add("AUDIO_ROUTE_UNVERIFIED");
            }
            JsonElement input = report.GetProperty("input");
            long frequency = Number(report, "timestamp_frequency");
            if (frequency <= 0)
            {
                throw new InvalidDataException("Timestamp frequency must be positive.");
            }
            long effect = Number(report, "expected_effect");
            string actor = Text(input, "actor_id");
            string target = Text(input, "target_id");
            string ability = Text(input, "ability");
            long action = Number(input, "action_id");
            long nativeAction = OptionalNumber(report, "expected_native_action") ?? action;
            long? nativeCommand = OptionalNumber(report, "expected_native_command");
            HashSet<long> supportingEffects = report.TryGetProperty("supporting_effects", out JsonElement supporting) ? supporting.EnumerateArray().Select(value => value.GetInt64()).ToHashSet() : [];
            if (nativeAction is < 0 or > ushort.MaxValue || nativeCommand is < 0 or > byte.MaxValue || OptionalNumber(report, "expected_native_action").HasValue != nativeCommand.HasValue || supportingEffects.Any(value => value is < 1 or > ushort.MaxValue || value == effect))
            {
                errors.Add("NATIVE_INVOCATION_IDENTITY_INVALID");
            }
            if (effect is < 1 or > ushort.MaxValue || string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(target) || Flag(input, "draw_cast"))
            {
                errors.Add("INPUT_UNSUPPORTED");
            }
            if (recipe is not null && (recipe.RequestedEffectId != effect || recipe.ActionId != $"action{action}" || recipe.Executions != 2 || recipe.Blockers.Length != 0))
            {
                errors.Add("SEALED_CASE_MISMATCH_OR_BLOCKED");
            }
            JsonElement executions = report.GetProperty("executions");
            if (executions.GetArrayLength() != 2)
            {
                errors.Add("EXACTLY_TWO_EXECUTIONS_REQUIRED");
            }
            TelemetryContinuityGuard? continuity = null;
            long previousSerial = 0;
            long previousEndCallback = -1;
            long previousEndFrame = -1;
            int index = 0;
            foreach (JsonElement execution in executions.EnumerateArray())
            {
                index++;
                if (Number(execution, "index") != index || !Flag(execution, "completed") || !Flag(execution, "cleanup_completed") || Number(execution, "tail_presented_frames") < 2)
                {
                    errors.Add("COMPLETION_UNVERIFIED");
                }
                long startCallback = Number(execution, "start_callback");
                long endCallback = Number(execution, "end_callback");
                long startFrame = Number(execution, "start_frame");
                long endFrame = Number(execution, "end_frame");
                if (startCallback < previousEndCallback || startFrame < previousEndFrame || endCallback <= startCallback || endFrame <= startFrame || Number(execution, "renderer_frames") <= 0)
                {
                    errors.Add("PRESENTATION_OR_CONTINUITY_UNVERIFIED");
                }
                previousEndCallback = endCallback;
                previousEndFrame = endFrame;
                TelemetryOwnerState start = execution.GetProperty("start_owners").Deserialize<TelemetryOwnerState>(Json)!;
                TelemetryOwnerState end = execution.GetProperty("end_owners").Deserialize<TelemetryOwnerState>(Json)!;
                if (start.BattleInstanceId != Text(report, "battle_instance_id") || start.ConstructorCount != 1 || start.RestoreCount != 0 || start.ResetCount != 0 || start.NativeCallback != startCallback || end.NativeCallback != endCallback || start.FrameIndex != startFrame || end.FrameIndex != endFrame || string.IsNullOrWhiteSpace(start.RetainedMemoryOwnerId) || string.IsNullOrWhiteSpace(start.RendererOwnerId))
                {
                    errors.Add("CONTINUITY_BROKEN");
                }
                continuity ??= new(start);
                try
                {
                    continuity.Observe(start);
                    continuity.Observe(end);
                }
                catch (InvalidDataException)
                {
                    errors.Add("CONTINUITY_BROKEN");
                }
                JsonElement dispatches = execution.GetProperty("dispatches");
                if (dispatches.EnumerateArray().Count(dispatch => Number(dispatch, "requested_effect") == effect) != 1)
                {
                    errors.Add("DISPATCH_COUNT_INVALID");
                }
                foreach (JsonElement dispatch in dispatches.EnumerateArray())
                {
                    long serial = Number(dispatch, "serial");
                    long callback = Number(dispatch, "callback");
                    bool focal = Number(dispatch, "requested_effect") == effect;
                    bool invocationMatches = focal ? Text(dispatch, "ability") == ability && Number(dispatch, "action_id") == nativeAction && (nativeCommand is null || OptionalNumber(dispatch, "native_command") == nativeCommand) : supportingEffects.Contains(Number(dispatch, "requested_effect"));
                    if (serial <= previousSerial || callback < startCallback || callback > endCallback || Text(dispatch, "actor_id") != actor || !invocationMatches || Number(dispatch, "executed_effect") != Number(dispatch, "requested_effect"))
                    {
                        errors.Add("DISPATCH_IDENTITY_MISMATCH");
                    }
                    previousSerial = serial;
                }
                TelemetryFrameSummary supplied = execution.GetProperty("frames").Deserialize<TelemetryFrameSummary>(Json)!;
                TelemetryFrameSummary recomputed = TelemetryFrameCollector.Recompute(supplied.Intervals.ToArray(), supplied.DroppedSamples);
                frames.Add(recomputed);
                long previousTicks = Number(execution, "preceding_presentation_ticks");
                long previousFrame = startFrame;
                long previousCallback = startCallback;
                foreach (TelemetryFrameInterval interval in recomputed.Intervals)
                {
                    double measuredDuration = checked(interval.End.TimestampTicks - previousTicks) * 1000.0 / frequency;
                    if (interval.End.TimestampTicks <= previousTicks || interval.End.FrameIndex != previousFrame + 1 || interval.End.FrameIndex > endFrame || interval.End.NativeCallbacks < previousCallback || interval.End.NativeCallbacks > endCallback || Math.Abs(interval.DurationMs - measuredDuration) > 0.0000001)
                    {
                        errors.Add("FRAME_LINEAGE_INVALID");
                    }
                    previousTicks = interval.End.TimestampTicks;
                    previousFrame = interval.End.FrameIndex;
                    previousCallback = interval.End.NativeCallbacks;
                }
                if (previousFrame != endFrame || recomputed.FrameCount != endFrame - startFrame)
                {
                    errors.Add("FRAME_COVERAGE_INCOMPLETE");
                }
                long effectStarted = Number(execution, "effect_started_ticks");
                long effectCompleted = Number(execution, "effect_completed_ticks");
                double effectDuration = checked(effectCompleted - effectStarted) * 1000.0 / frequency;
                double declaredDuration = execution.GetProperty("effect_duration_ms").GetDouble();
                if (effectStarted < Number(execution, "preceding_presentation_ticks") || effectCompleted <= effectStarted || effectCompleted > previousTicks || !double.IsFinite(declaredDuration) || Math.Abs(declaredDuration - effectDuration) > 0.0000001 || effectDuration > recomputed.DurationMs)
                {
                    errors.Add("EFFECT_TIMING_MARKERS_INVALID");
                }
                else
                {
                    effectDurations.Add(effectDuration);
                }
                if (!recomputed.Valid || Number(execution, "dropped_events") != 0)
                {
                    errors.Add("TELEMETRY_LOSS_OR_EMPTY");
                }
                if (recomputed.SlowIntervalCount != 0)
                {
                    errors.Add("PRESENTATION_BELOW_30_FPS");
                }
                ValidateOptionalTraffic(report, execution, effect, diagnostics);
                if (execution.GetProperty("integrity_error").ValueKind != JsonValueKind.Null)
                {
                    errors.Add("INTEGRITY_CHECK_FAILED");
                }
                ValidateSemantics(execution, actor, target, ability, action, startCallback, endCallback, errors);
            }
            ValidateRepairs(report, executions, errors);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or ArgumentException or NullReferenceException or FormatException or OverflowException)
        {
            errors.Add("REPORT_INVALID: " + exception.Message);
        }
        string[] distinct = errors.Distinct(StringComparer.Ordinal).ToArray();
        string verdict = distinct.Length == 0 ? "PASS" : distinct.All(error => error == "PRESENTATION_BELOW_30_FPS") ? "FAIL" : "ERROR";
        return new(verdict, distinct, frames.ToArray(), effectDurations.ToArray(), frames.Count == 0 ? null : TelemetryFrameCollector.Combine(frames.ToArray()), false, "Configured non-muted native receipt routing; auditory equivalence is not certified.", diagnostics.Distinct().ToArray(), "Optional shared-operation payload counters are partial lower bounds. Complete effect-local/process copy traffic is not measured; unavailable is not zero.");
    }

    private static void ValidateRepairs(JsonElement report, JsonElement executions, List<string> errors)
    {
        if (!report.TryGetProperty("repairs", out JsonElement receipts) || receipts.GetArrayLength() == 0)
        {
            if (report.TryGetProperty("declared_repairs", out JsonElement missing) && missing.GetArrayLength() != 0)
            {
                errors.Add("REPAIR_EVIDENCE_INVALID");
            }
            return;
        }
        if (executions.GetArrayLength() != 2 || receipts.GetArrayLength() != 1 || !report.TryGetProperty("declared_repairs", out JsonElement declared) || declared.GetArrayLength() is < 1 or > 3)
        {
            errors.Add("REPAIR_EVIDENCE_INVALID");
            return;
        }
        JsonElement receipt = receipts[0];
        long callback = Number(receipt, "callback_before");
        if (callback != Number(receipt, "callback_after") || callback < Number(executions[0], "end_callback") || callback > Number(executions[1], "start_callback") || Number(receipt, "frame") < Number(executions[0], "end_frame") || Number(receipt, "frame") > Number(executions[1], "start_frame") || Number(receipt, "restore_count") != 0 || Number(receipt, "reset_count") != 0 || Text(receipt, "lifecycle_id") != Text(executions[0].GetProperty("start_owners"), "retained_memory_owner_id") || !JsonElement.DeepEquals(receipt.GetProperty("random_before"), receipt.GetProperty("random_after")) || !JsonElement.DeepEquals(receipt.GetProperty("items_before"), receipt.GetProperty("items_after")))
        {
            errors.Add("REPAIR_OWNER_CONTINUITY_BROKEN");
        }
        JsonElement[] changes = receipt.GetProperty("changes").EnumerateArray().ToArray();
        if (changes.Length != declared.GetArrayLength())
        {
            errors.Add("REPAIR_DECLARATION_MISMATCH");
        }
        Dictionary<string, JsonElement> firstAfter = executions[0].GetProperty("after").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, JsonElement> secondBefore = executions[1].GetProperty("before").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        HashSet<string> declaredActors = declared.EnumerateArray().Select(request => Text(request, "actor_id")).ToHashSet(StringComparer.Ordinal);
        HashSet<string> changedActors = new(StringComparer.Ordinal);
        if (declaredActors.Count != declared.GetArrayLength())
        {
            errors.Add("REPAIR_DECLARATION_MISMATCH");
        }
        foreach (JsonElement change in changes)
        {
            JsonElement request = change.GetProperty("request");
            JsonElement before = change.GetProperty("before");
            JsonElement after = change.GetProperty("after");
            string actor = Text(request, "actor_id");
            if (!changedActors.Add(actor) || !declared.EnumerateArray().Any(value => JsonElement.DeepEquals(value, request)) || Text(before, "id") != actor || Text(after, "id") != actor || Number(after, "current_hp") != Number(request, "hit_points") || Number(before, "maximum_hp") != Number(after, "maximum_hp") || !JsonElement.DeepEquals(before.GetProperty("magic_stocks"), after.GetProperty("magic_stocks")))
            {
                errors.Add("REPAIR_DECLARATION_MISMATCH");
            }
            if (!firstAfter.TryGetValue(actor, out JsonElement first) || !secondBefore.TryGetValue(actor, out JsonElement second) || !JsonElement.DeepEquals(first, before) || !JsonElement.DeepEquals(second, after))
            {
                errors.Add("REPAIR_STATE_LINEAGE_INVALID");
            }
            Memoria.FF8.Runtime.BattleDiagnosticCombatRepair repair = request.Deserialize<Memoria.FF8.Runtime.BattleDiagnosticCombatRepair>(RecordJson.Options) ?? throw new InvalidDataException("Repair request is null.");
            try
            {
                repair.Validate();
            }
            catch (InvalidDataException)
            {
                errors.Add("REPAIR_DECLARATION_MISMATCH");
                continue;
            }
            long beforeSaved = Number(before, "statuses0");
            long beforeBattle = Number(before, "statuses1");
            long maximum = Number(before, "maximum_hp");
            long expectedSaved = (beforeSaved & ~(repair.RemoveStatuses0 | (repair.Revive ? 1 : 0))) & 0xFCFF;
            // The ordinary HP commit recomputes saved low-HP bits after the declared removals.
            if (repair.HitPoints < (maximum >> 1))
            {
                expectedSaved |= repair.HitPoints < (maximum >> 2) ? 0x300L : 0x200L;
            }
            if (beforeSaved is < 0 or > ushort.MaxValue || beforeBattle is < 0 or > uint.MaxValue || repair.HitPoints > maximum || !repair.Revive && (Number(before, "current_hp") == 0 || (beforeSaved & 1) != 0) || Number(after, "statuses0") != expectedSaved || Number(after, "statuses1") != (beforeBattle & ~repair.RemoveStatuses1))
            {
                errors.Add("REPAIR_STATUS_MUTATION_INVALID");
            }
        }
        if (!changedActors.SetEquals(declaredActors))
        {
            errors.Add("REPAIR_DECLARATION_MISMATCH");
        }
    }

    private static void ValidateOptionalTraffic(JsonElement report, JsonElement execution, long effect, List<string> diagnostics)
    {
        try
        {
            if (!report.TryGetProperty("coverage", out JsonElement manifest) || manifest.ValueKind == JsonValueKind.Null || !execution.TryGetProperty("traffic", out JsonElement counters) || counters.ValueKind == JsonValueKind.Null)
            {
                diagnostics.Add("COPY_TRAFFIC_NOT_MEASURED");
                return;
            }
            TelemetryCoverageManifest coverage = manifest.Deserialize<TelemetryCoverageManifest>(Json)!;
            if (!coverage.MandatoryCoverageAvailable || effect is < 1 or > ushort.MaxValue)
            {
                diagnostics.Add("COPY_TRAFFIC_UNAVAILABLE");
                return;
            }
            TelemetryCoverageManifest authoritative = TelemetryCoverageManifest.ManagedPayload((ushort)effect);
            if (coverage.InstrumentationVersion != authoritative.InstrumentationVersion || !coverage.Sites.OrderBy(site => site.SiteId).SequenceEqual(authoritative.Sites.OrderBy(site => site.SiteId)))
            {
                diagnostics.Add("COPY_TELEMETRY_MANIFEST_MISMATCH");
                return;
            }
            TelemetryTrafficSnapshot traffic = counters.Deserialize<TelemetryTrafficSnapshot>(Json)!;
            HashSet<(int SiteId, TelemetryTrafficKind Kind)> expected = coverage.Sites.SelectMany(site => Enum.GetValues<TelemetryTrafficKind>().Select(kind => (site.SiteId, kind))).ToHashSet();
            HashSet<(int SiteId, TelemetryTrafficKind Kind)> actual = traffic.Sites.Select(site => (site.SiteId, site.Kind)).ToHashSet();
            if (!traffic.Available || traffic.Error is not null || actual.Count != traffic.Sites.Count || !actual.SetEquals(expected) || traffic.Sites.Any(site => site.Bytes < 0 || site.Operations < 0 || site.Operations == 0 && site.Bytes != 0 || site.LargestOperationBytes < 0 || site.LargestOperationBytes > site.Bytes))
            {
                diagnostics.Add("COPY_TRAFFIC_INVALID_OR_INCOMPLETE");
                return;
            }
            diagnostics.Add("COPY_TRAFFIC_PARTIAL_SHARED_OPERATIONS");
            if (traffic.RequiresLargeCopyReview)
            {
                diagnostics.Add("LARGE_COPY_REVIEW_REQUIRED");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException)
        {
            diagnostics.Add("COPY_TRAFFIC_INVALID_OR_INCOMPLETE");
        }
    }

    private static void ValidateSemantics(JsonElement execution, string actor, string requestedTarget, string ability, long action, long startCallback, long endCallback, List<string> errors)
    {
        Dictionary<string, JsonElement> beforeStates = execution.GetProperty("before").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, JsonElement> afterStates = execution.GetProperty("after").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, long> observedHp = beforeStates.ToDictionary(pair => pair.Key, pair => Number(pair.Value, "current_hp"), StringComparer.Ordinal);
        JsonElement[] resolutions = execution.GetProperty("resolutions").EnumerateArray().ToArray();
        bool completed = execution.GetProperty("events").EnumerateArray().Any(value => Text(value, "kind") == "ActionComplete" && Text(value, "actor_id") == actor);
        if (!completed)
        {
            errors.Add("SEMANTIC_RESOLUTION_UNVERIFIED");
        }
        if (resolutions.Where(resolution => Text(resolution, "actor_id") == actor).Any(resolution => Text(resolution, "original_target_id") != requestedTarget))
        {
            errors.Add("FOCAL_RESOLUTION_TARGET_MISMATCH");
        }
        Dictionary<string, long> deltas = [];
        foreach (JsonElement resolution in resolutions)
        {
            long actual = Number(resolution, "actual_amount");
            long resolved = Number(resolution, "resolved_amount");
            long before = Number(resolution, "before_hp");
            long after = Number(resolution, "after_hp");
            long callback = Number(resolution, "callback");
            string target = Text(resolution, "actual_target_id");
            string kind = Text(resolution, "kind");
            if (!beforeStates.ContainsKey(Text(resolution, "actor_id")))
            {
                errors.Add("RESOLUTION_ACTOR_UNKNOWN");
            }
            long delta = after - before;
            bool healing = kind.Contains("heal", StringComparison.OrdinalIgnoreCase);
            bool damage = kind.Contains("damage", StringComparison.OrdinalIgnoreCase);
            if (actual < 0 || resolved < actual || before < 0 || after < 0 || callback < startCallback || callback > endCallback || string.IsNullOrWhiteSpace(Text(resolution, "hp_owner")) || string.IsNullOrWhiteSpace(Text(resolution, "original_target_id")) || !healing && !damage || delta != (healing ? actual : -actual))
            {
                errors.Add("SEMANTIC_RESOLUTION_INVALID");
            }
            if (!observedHp.TryGetValue(target, out long previousHp) || previousHp != before)
            {
                errors.Add("RESOLUTION_HP_LINEAGE_INVALID");
            }
            observedHp[target] = after;
            deltas[target] = checked(deltas.GetValueOrDefault(target) + delta);
        }
        foreach ((string id, JsonElement before) in beforeStates)
        {
            if (!afterStates.TryGetValue(id, out JsonElement after) || Number(after, "maximum_hp") != Number(before, "maximum_hp") || Number(after, "current_hp") < 0 || Number(after, "current_hp") > Number(after, "maximum_hp") || Number(before, "current_hp") < 0 || Number(before, "current_hp") > Number(before, "maximum_hp") || Number(after, "current_hp") - Number(before, "current_hp") != deltas.GetValueOrDefault(id))
            {
                errors.Add("HP_RECONCILIATION_FAILED");
            }
        }
        if (afterStates.Count != beforeStates.Count || deltas.Keys.Any(id => !beforeStates.ContainsKey(id)))
        {
            errors.Add("RECIPIENT_OWNER_MISMATCH");
        }
        if (ability == "Magic")
        {
            if (!beforeStates.TryGetValue(actor, out JsonElement before) || !afterStates.TryGetValue(actor, out JsonElement after) || Stock(before, action) - Stock(after, action) != 1)
            {
                errors.Add("SPELL_STOCK_CONSUMPTION_UNVERIFIED");
            }
        }
    }

    private static long Stock(JsonElement state, long action) => state.GetProperty("magic_stocks").EnumerateArray().Where(stock => Number(stock, "magic_id") == action).Sum(stock => Number(stock, "quantity"));
    private static long Number(JsonElement value, string name) => value.GetProperty(name).GetInt64();
    private static long? OptionalNumber(JsonElement value, string name) => value.TryGetProperty(name, out JsonElement number) && number.ValueKind != JsonValueKind.Null ? number.GetInt64() : null;
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException($"Missing string {name}.");
    private static bool Flag(JsonElement value, string name) => value.GetProperty(name).GetBoolean();

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("Duplicate JSON property.");
                }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement element in value.EnumerateArray())
            {
                RejectDuplicateProperties(element);
            }
        }
    }
}
