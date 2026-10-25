using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.BattleEffects;

public sealed record LiveEvidenceVerdict(string Verdict, string[] Errors, TelemetryFrameSummary[] Executions, TelemetryFrameSummary? Combined, bool CampaignAcceptance, string AudioScope);

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
        List<TelemetryFrameSummary> frames = [];
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
            TelemetryCoverageManifest coverage = report.GetProperty("coverage").Deserialize<TelemetryCoverageManifest>(Json) ?? throw new InvalidDataException("Missing coverage.");
            if (!coverage.MandatoryCoverageAvailable)
            {
                errors.Add("TELEMETRY_UNAVAILABLE");
            }
            JsonElement input = report.GetProperty("input");
            long frequency = Number(report, "timestamp_frequency");
            if (frequency <= 0)
            {
                throw new InvalidDataException("Timestamp frequency must be positive.");
            }
            long effect = Number(report, "expected_effect");
            string actor = Text(input, "actor_id");
            string ability = Text(input, "ability");
            long action = Number(input, "action_id");
            if (effect is < 1 or > ushort.MaxValue || string.IsNullOrWhiteSpace(actor) || Flag(input, "draw_cast"))
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
                if (dispatches.GetArrayLength() != 1)
                {
                    errors.Add("DISPATCH_COUNT_INVALID");
                }
                foreach (JsonElement dispatch in dispatches.EnumerateArray())
                {
                    long serial = Number(dispatch, "serial");
                    long callback = Number(dispatch, "callback");
                    if (serial <= previousSerial || callback < startCallback || callback > endCallback || Text(dispatch, "actor_id") != actor || Text(dispatch, "ability") != ability || Number(dispatch, "action_id") != action || Number(dispatch, "requested_effect") != effect || Number(dispatch, "executed_effect") != effect)
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
                    double measuredDuration = (interval.End.TimestampTicks - previousTicks) * 1000.0 / frequency;
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
                if (!recomputed.Valid || Number(execution, "dropped_events") != 0)
                {
                    errors.Add("TELEMETRY_LOSS_OR_EMPTY");
                }
                if (recomputed.SlowIntervalCount != 0)
                {
                    errors.Add("PRESENTATION_BELOW_30_FPS");
                }
                TelemetryTrafficSnapshot traffic = execution.GetProperty("traffic").Deserialize<TelemetryTrafficSnapshot>(Json)!;
                if (!traffic.Available || traffic.Error is not null || coverage.Sites.Any(site => site.Mandatory && !traffic.Sites.Any(aggregate => aggregate.SiteId == site.SiteId)) || traffic.Sites.Select(site => (site.SiteId, site.Kind)).Distinct().Count() != traffic.Sites.Count || traffic.Sites.Any(site => !Enum.IsDefined(site.Kind) || site.Bytes < 0 || site.Operations < 0 || site.Operations == 0 && site.Bytes != 0 || site.LargestOperationBytes < 0 || site.LargestOperationBytes > site.Bytes || !coverage.Sites.Any(registered => registered.SiteId == site.SiteId && registered.Enabled)))
                {
                    errors.Add("TRAFFIC_UNAVAILABLE_OR_INVALID");
                }
                if (traffic.RequiresLargeCopyReview)
                {
                    errors.Add("LARGE_COPY_REVIEW_REQUIRED");
                }
                if (execution.GetProperty("integrity_error").ValueKind != JsonValueKind.Null)
                {
                    errors.Add("INTEGRITY_CHECK_FAILED");
                }
                ValidateSemantics(execution, actor, ability, action, startCallback, endCallback, errors);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or ArgumentException or NullReferenceException or FormatException or OverflowException)
        {
            errors.Add("REPORT_INVALID: " + exception.Message);
        }
        string[] distinct = errors.Distinct(StringComparer.Ordinal).ToArray();
        string verdict = distinct.Length == 0 ? "PASS" : distinct.All(error => error == "PRESENTATION_BELOW_30_FPS") ? "FAIL" : "ERROR";
        return new(verdict, distinct, frames.ToArray(), frames.Count == 0 ? null : TelemetryFrameCollector.Combine(frames.ToArray()), false, "Configured non-muted native receipt routing; auditory equivalence is not certified.");
    }

    private static void ValidateSemantics(JsonElement execution, string actor, string ability, long action, long startCallback, long endCallback, List<string> errors)
    {
        Dictionary<string, JsonElement> beforeStates = execution.GetProperty("before").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, JsonElement> afterStates = execution.GetProperty("after").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, long> observedHp = beforeStates.ToDictionary(pair => pair.Key, pair => Number(pair.Value, "current_hp"), StringComparer.Ordinal);
        JsonElement[] resolutions = execution.GetProperty("resolutions").EnumerateArray().ToArray();
        if (execution.GetProperty("events").GetArrayLength() == 0 || !resolutions.Any(resolution => Text(resolution, "actor_id") == actor && Number(resolution, "resolved_amount") > 0))
        {
            errors.Add("SEMANTIC_RESOLUTION_UNVERIFIED");
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
