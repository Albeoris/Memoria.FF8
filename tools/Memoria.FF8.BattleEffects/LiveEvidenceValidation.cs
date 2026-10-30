using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public sealed record LiveEvidenceVerdict(string Verdict, string[] Errors, TelemetryFrameSummary[] Executions, double[] EffectDurationsMs, TelemetryFrameSummary? Combined, bool CampaignAcceptance, string AudioScope, string[] DiagnosticIssues, string CopyTrafficScope)
{
    public TelemetryEffectFrameSummary[] EffectExecutions { get; init; } = [];
    public int ReflectedChildDispatches { get; init; }
}

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

    public static LiveEvidenceVerdict Validate(JsonElement report, CaseRecipe? recipe = null) => ValidateCore(report, recipe, false);

    public static LiveEvidenceVerdict ValidateFreshEntry(JsonElement report) => ValidateCore(report, null, true);

    private static LiveEvidenceVerdict ValidateCore(JsonElement report, CaseRecipe? recipe, bool freshEntry)
    {
        List<string> errors = [];
        List<string> diagnostics = [];
        List<TelemetryFrameSummary> frames = [];
        List<TelemetryEffectFrameSummary> effectFrames = [];
        List<double> effectDurations = [];
        int reflectedChildDispatches = 0;
        HashSet<(int Header, long Generation)> reflectedHeaders = [];
        try
        {
            RejectDuplicateProperties(report);
            PreludeEvidenceValidation.ValidateReport(report);
            FreshEntryPairValidation.ValidateProtocol(report, freshEntry);
            if (freshEntry)
            {
                SlotsLiveEvidenceValidation.ValidateFreshEntryReport(report);
            }
            else
            {
                SlotsLiveEvidenceValidation.ValidateReport(report);
            }
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
            if (!Flag(environment, "gpu") || !Flag(environment, "real_time") || Number(environment, "width") != 1920 || Number(environment, "height") != 1080 || string.IsNullOrWhiteSpace(Text(environment, "renderer")))
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
            DiagnosticShotCycleDeclaration? shotCycle = ReadShotDeclaration(report, input, effect, nativeAction, nativeCommand);
            DiagnosticCounterCycleDeclaration? counterCycle = ReadCounterDeclaration(report, input, effect, nativeAction, nativeCommand);
            DiagnosticCounterCycleReceipt[] counterCycles = report.TryGetProperty("counter_cycles", out JsonElement counterReceipts) ? counterReceipts.Deserialize<DiagnosticCounterCycleReceipt[]>(Json) ?? throw new InvalidDataException("Counter receipts require an explicit array.") : [];
            if (counterCycle is null ? counterCycles.Length != 0 : counterCycles.Length != 2)
            {
                throw new InvalidDataException("Counter declaration and exactly two source-proved cycles must be present together.");
            }
            if (counterCycle is not null)
            {
                BattleInput controlInput = input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Counter trigger input is absent.");
                DiagnosticCounterCycleValidation.ValidateChain(counterCycles[0], counterCycles[1], controlInput);
                if (counterCycles.Any(cycle => cycle.Declaration != counterCycle))
                {
                    throw new InvalidDataException("Counter cycle changed its declared focal protocol.");
                }
            }
            DiagnosticShotCycleReceipt[] shotCycles = report.TryGetProperty("shot_cycles", out JsonElement cycles) ? cycles.Deserialize<DiagnosticShotCycleReceipt[]>(Json) ?? throw new InvalidDataException("Shot receipts require an explicit array.") : [];
            if (shotCycle is null ? shotCycles.Length != 0 : shotCycles.Length != 2)
            {
                throw new InvalidDataException("Shot declaration and exactly two cycle receipts must be present together.");
            }
            if (shotCycle is not null)
            {
                DiagnosticShotCycleValidation.ValidateChain(shotCycles[0], shotCycles[1]);
                JsonElement preparation = report.GetProperty("initialization").GetProperty("preparation");
                JsonElement originalDriver = preparation.GetProperty("driver");
                BattleDiagnosticLimitSelection selectionPolicy = originalDriver.GetProperty("limit_selection").Deserialize<BattleDiagnosticLimitSelection>(Json) ?? throw new InvalidDataException("Shot cycle requires its initialized ordinary Limit selection policy.");
                selectionPolicy.Validate();
                JsonElement[] ammunition = preparation.GetProperty("fixture").GetProperty("items").EnumerateArray().Where(item => Number(item, "id") == 101 + shotCycle.AmmoAction).ToArray();
                if (ammunition.Length != 1 || Number(ammunition[0], "count") is < 3 or > 100 || Number(ammunition[0], "count") != shotCycles[0].BeforeOpening.Inventory)
                {
                    throw new InvalidDataException("Shot finite ammunition does not match the initialized item preparation.");
                }
                foreach (DiagnosticShotCycleReceipt cycle in shotCycles)
                {
                    if (cycle.Selection.CrisisSelections.Length > selectionPolicy.MaximumSelections || cycle.Selection.CrisisSelections[^1] < selectionPolicy.MinimumCrisisLevel || cycle.Selection.CrisisSelections[^1] > selectionPolicy.MaximumCrisisLevel)
                    {
                        throw new InvalidDataException("Shot Limit selection exceeds its declared ordinary selection bounds.");
                    }
                }
                JsonElement selections = report.GetProperty("limit_selections");
                if (selections.GetArrayLength() != 2 || report.GetProperty("supporting_effects").GetArrayLength() != 0 || report.GetProperty("supporting_invocations").GetArrayLength() != 0 || report.GetProperty("declared_repairs").GetArrayLength() != 0 || report.GetProperty("declared_terminal_hold").ValueKind != JsonValueKind.Null)
                {
                    throw new InvalidDataException("Shot cycles cannot use supporting dispatch bypasses, repairs or terminal holds.");
                }
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    if (shotCycles[cycle].Declaration != shotCycle || !JsonElement.DeepEquals(selections[cycle], JsonSerializer.SerializeToElement(shotCycles[cycle].Selection, Json)))
                    {
                        throw new InvalidDataException("Shot cycle declaration or real Limit selection receipt differs from the report.");
                    }
                }
            }
            HashSet<long> supportingEffects = report.TryGetProperty("supporting_effects", out JsonElement supporting) ? supporting.EnumerateArray().Select(value => value.GetInt64()).ToHashSet() : [];
            BattleDiagnosticSupportingInvocation[] supportingInvocations = ReadSupportingInvocations(report, actor, effect);
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
            if (executions.GetArrayLength() != (freshEntry ? 1 : 2))
            {
                errors.Add(freshEntry ? "EXACTLY_ONE_FRESH_ENTRY_EXECUTION_REQUIRED" : "EXACTLY_TWO_EXECUTIONS_REQUIRED");
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
                BattleDiagnosticEffectDispatch[] typedDispatches = dispatches.Deserialize<BattleDiagnosticEffectDispatch[]>(Json) ?? throw new InvalidDataException("Dispatch observations must be an explicit array.");
                BattleDiagnosticInvocationRelease? invocationRelease = null;
                if (freshEntry)
                {
                    invocationRelease = ValidateFreshInvocationRelease(report, execution, typedDispatches, startCallback, endCallback, endFrame);
                }
                else if (execution.TryGetProperty("invocation_release", out JsonElement unexpectedRelease) && unexpectedRelease.ValueKind != JsonValueKind.Null)
                {
                    throw new InvalidDataException("Ordinary two-use evidence cannot substitute a focal release for global drain.");
                }
                if (shotCycle is not null)
                {
                    BattleInput submitted = input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Shot input is absent.");
                    DiagnosticShotCycleReceipt cycle = shotCycles[index - 1];
                    DiagnosticShotCycleValidation.ValidateReceipt(cycle, submitted, checked((ushort)effect), typedDispatches, startCallback, endCallback);
                    foreach (string boundary in new[] { "before", "after" })
                    {
                        JsonElement[] states = execution.GetProperty(boundary).EnumerateArray().ToArray();
                        JsonElement[] actorStates = states.Where(state => Text(state, "id") == actor).ToArray();
                        JsonElement[] targetStates = states.Where(state => Text(state, "id") == target).ToArray();
                        if (actorStates.Length != 1 || targetStates.Length != 1 || OptionalNumber(actorStates[0], "native_index") != cycle.ActorIndex || OptionalNumber(targetStates[0], "native_index") != cycle.TargetIndex)
                        {
                            throw new InvalidDataException("Shot packet/recipient identities differ from the actual execution combatant ledger.");
                        }
                    }
                }
                if (counterCycle is not null)
                {
                    DiagnosticCounterCycleReceipt cycle = counterCycles[index - 1];
                    if (cycle.Execution != index || cycle.StartedCallback < startCallback || cycle.CompletedCallback > endCallback || !typedDispatches.Any(dispatch => dispatch == cycle.Attempts[^1].Dispatch))
                    {
                        throw new InvalidDataException("Counter cycle source receipts differ from the actual execution/dispatch boundary.");
                    }
                    foreach (var contact in cycle.Attempts.SelectMany(attempt => attempt.Contacts))
                    {
                        var primary = contact.Primary;
                        bool resolved = execution.GetProperty("resolutions").EnumerateArray().Any(resolution => Number(resolution, "callback") == primary.Callback && Text(resolution, "actor_id") == primary.ActorId && Text(resolution, "actual_target_id") == primary.RecipientId && Number(resolution, "before_hp") == primary.BeforeHp && Number(resolution, "after_hp") == primary.AfterHp);
                        if (!resolved)
                        {
                            throw new InvalidDataException("Counter committed primary HP source is absent from the actual native resolution ledger.");
                        }
                    }
                    foreach (string boundary in new[] { "before", "after" })
                    {
                        JsonElement[] states = execution.GetProperty(boundary).EnumerateArray().ToArray();
                        JsonElement[] controlStates = states.Where(state => Text(state, "id") == actor).ToArray();
                        JsonElement[] focalStates = states.Where(state => Text(state, "id") == cycle.TargetId).ToArray();
                        if (controlStates.Length != 1 || focalStates.Length != 1 || OptionalNumber(controlStates[0], "native_index") != cycle.ActorIndex || OptionalNumber(focalStates[0], "native_index") != cycle.TargetIndex || OptionalNumber(focalStates[0], "model_id") != 84)
                        {
                            throw new InvalidDataException("Counter physical control/focal identities differ from the actual battle execution ledger.");
                        }
                    }
                }
                if (typedDispatches.Count(dispatch => dispatch.RequestedEffect == effect && dispatch.Reflection is null && (shotCycle is null || dispatch.NativeCommand == 237)) != 1)
                {
                    errors.Add("DISPATCH_COUNT_INVALID");
                }
                foreach (JsonElement dispatch in dispatches.EnumerateArray())
                {
                    long serial = Number(dispatch, "serial");
                    long callback = Number(dispatch, "callback");
                    BattleDiagnosticEffectDispatch typed = dispatch.Deserialize<BattleDiagnosticEffectDispatch>(Json) ?? throw new InvalidDataException("Dispatch observation is null.");
                    bool focal = typed.RequestedEffect == effect && typed.Reflection is null && (shotCycle is null || typed.NativeCommand == 237);
                    bool sameActor = Text(dispatch, "actor_id") == actor;
                    bool invocationMatches;
                    if (shotCycle is not null)
                    {
                        invocationMatches = shotCycles[index - 1].Dispatches.Any(observed => observed == typed);
                    }
                    else if (counterCycle is not null)
                    {
                        invocationMatches = focal ? typed == counterCycles[index - 1].Attempts[^1].Dispatch : sameActor ? supportingEffects.Contains(Number(dispatch, "requested_effect")) : HasSupportingInvocation(execution, dispatch, supportingInvocations);
                    }
                    else if (typed.Reflection is { } reflection)
                    {
                        BattleDiagnosticEffectDispatch[] parents = typedDispatches.Where(parent => parent.Reflection is null && parent.RequestedEffect == effect && parent.ExecutedEffect == effect && parent.Serial < typed.Serial && parent.Callback <= typed.Callback && parent.ActorId == actor && parent.Ability?.ToString() == ability && parent.ActionId == nativeAction && (nativeCommand is null || parent.NativeCommand == nativeCommand) && BattleDiagnosticReflectionLineage.Matches(typed, parent)).ToArray();
                        invocationMatches = parents.Length == 1 && reflectedHeaders.Add((reflection.ReflectedHeaderIndex, reflection.ReflectedHeaderGeneration));
                    }
                    else
                    {
                        invocationMatches = focal ? sameActor && Text(dispatch, "ability") == ability && Number(dispatch, "action_id") == nativeAction && (nativeCommand is null || OptionalNumber(dispatch, "native_command") == nativeCommand) : sameActor ? supportingEffects.Contains(Number(dispatch, "requested_effect")) : HasSupportingInvocation(execution, dispatch, supportingInvocations);
                        if (typed.NativeCommand == 247 && (!focal || nativeCommand != 247))
                        {
                            invocationMatches = false;
                        }
                    }
                    if (serial <= previousSerial || callback < startCallback || callback > endCallback || !invocationMatches || Number(dispatch, "executed_effect") != Number(dispatch, "requested_effect"))
                    {
                        errors.Add("DISPATCH_IDENTITY_MISMATCH");
                    }
                    else if (typed.Reflection is not null)
                    {
                        reflectedChildDispatches++;
                    }
                    previousSerial = serial;
                }
                TelemetryFrameSummary supplied = execution.GetProperty("frames").Deserialize<TelemetryFrameSummary>(Json)!;
                TelemetryFrameSummary recomputed = TelemetryFrameCollector.Recompute(supplied.Intervals.ToArray(), supplied.DroppedSamples);
                frames.Add(recomputed);
                long previousTicks = Number(execution, "preceding_presentation_ticks");
                long previousFrame = startFrame;
                long previousCallback = startCallback;
                bool frameLineageValid = previousTicks >= 0;
                foreach (TelemetryFrameInterval interval in recomputed.Intervals)
                {
                    double measuredDuration = checked(interval.End.TimestampTicks - previousTicks) * 1000.0 / frequency;
                    if (interval.End.TimestampTicks <= previousTicks || interval.End.FrameIndex != previousFrame + 1 || interval.End.FrameIndex > endFrame || interval.End.NativeCallbacks < previousCallback || interval.End.NativeCallbacks > endCallback || Math.Abs(interval.DurationMs - measuredDuration) > 0.0000001)
                    {
                        errors.Add("FRAME_LINEAGE_INVALID");
                        frameLineageValid = false;
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
                if (effectStarted < Number(execution, "preceding_presentation_ticks") || effectCompleted <= effectStarted || (invocationRelease is null ? effectCompleted > previousTicks : effectCompleted != invocationRelease.Timestamp || previousTicks > invocationRelease.Timestamp) || !double.IsFinite(declaredDuration) || Math.Abs(declaredDuration - effectDuration) > 0.0000001 || (invocationRelease is null && effectDuration > recomputed.DurationMs))
                {
                    errors.Add("EFFECT_TIMING_MARKERS_INVALID");
                }
                else
                {
                    effectDurations.Add(effectDuration);
                    if (frameLineageValid)
                    {
                        effectFrames.Add(invocationRelease is null
                            ? TelemetryEffectFrames.Summarize(recomputed, Number(execution, "preceding_presentation_ticks"), effectStarted, effectCompleted, frequency)
                            : TelemetryEffectFrames.SummarizeNativeRelease(recomputed, Number(execution, "preceding_presentation_ticks"), effectStarted, effectCompleted, Number(execution.GetProperty("post_release_tail"), "first_timestamp"), frequency));
                    }
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
                BattleDiagnosticRepairReceipt[]? attemptRepairs = counterCycle is null ? null : counterCycles[index - 1].Attempts.Skip(1).Where(attempt => attempt.PreparationRepair is not null).Select(attempt => attempt.PreparationRepair!).ToArray();
                bool nativeRenzCompleted = ValidateNativeRenzokukenCompletion(report, execution, index, input, effect, nativeAction, nativeCommand);
                ValidateSemantics(execution, actor, target, ability, action, nativeAction, effect, startCallback, endCallback, errors, attemptRepairs, nativeRenzCompleted);
            }
            if (counterCycle is null)
            {
                ValidateRepairs(report, executions, errors);
                ValidateTerminalHolds(report, executions, errors, diagnostics);
            }
            else
            {
                ValidateCounterRepairs(report, executions, counterCycles);
                BattleDiagnosticRepairReceipt? intercycle = counterCycles[1].Attempts[0].PreparationRepair;
                JsonElement holdRepairs = JsonSerializer.SerializeToElement(intercycle is null ? Array.Empty<BattleDiagnosticRepairReceipt>() : new[] { intercycle }, Json);
                ValidateTerminalHolds(report, executions, errors, diagnostics, holdRepairs);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or ArgumentException or NullReferenceException or FormatException or OverflowException)
        {
            errors.Add("REPORT_INVALID: " + exception.Message);
        }
        string[] distinct = errors.Distinct(StringComparer.Ordinal).ToArray();
        string verdict = distinct.Length == 0 ? "PASS" : distinct.All(error => error == "PRESENTATION_BELOW_30_FPS") ? "FAIL" : "ERROR";
        return new(verdict, distinct, frames.ToArray(), effectDurations.ToArray(), frames.Count == 0 ? null : TelemetryFrameCollector.Combine(frames.ToArray()), false, "Configured non-muted native receipt routing; auditory equivalence is not certified.", diagnostics.Distinct().ToArray(), "Optional shared-operation payload counters are partial lower bounds. Complete effect-local/process copy traffic is not measured; unavailable is not zero.") { EffectExecutions = effectFrames.ToArray(), ReflectedChildDispatches = reflectedChildDispatches };
    }

    private static DiagnosticShotCycleDeclaration? ReadShotDeclaration(JsonElement report, JsonElement input, long effect, long action, long? command)
    {
        bool declared = report.TryGetProperty("shot_cycle", out JsonElement value) && value.ValueKind != JsonValueKind.Null;
        bool originalDeclared = false;
        JsonElement original = default;
        if (report.TryGetProperty("initialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.TryGetProperty("driver", out JsonElement driver))
        {
            originalDeclared = driver.TryGetProperty("shot_cycle", out original) && original.ValueKind != JsonValueKind.Null;
            if (originalDeclared && (!JsonElement.DeepEquals(input, driver.GetProperty("input")) || Number(driver, "expected_effect") != effect || OptionalNumber(driver, "expected_native_action") != action || OptionalNumber(driver, "expected_native_command") != command))
            {
                throw new InvalidDataException("Shot semantic input or native identity differs from the initialized request.");
            }
        }
        if (declared != originalDeclared || declared && !JsonElement.DeepEquals(value, original))
        {
            throw new InvalidDataException("Shot declaration differs from the initialized request.");
        }
        if (!declared)
        {
            return null;
        }
        DiagnosticShotCycleDeclaration declaration = value.Deserialize<DiagnosticShotCycleDeclaration>(Json) ?? throw new InvalidDataException("Shot cycle declaration is empty.");
        declaration.Validate(input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Shot semantic input is missing."), checked((ushort)effect), checked((int)action), command is null ? null : checked((byte)command));
        return declaration;
    }

    private static DiagnosticCounterCycleDeclaration? ReadCounterDeclaration(JsonElement report, JsonElement input, long effect, long action, long? command)
    {
        bool declared = report.TryGetProperty("counter_cycle", out JsonElement value) && value.ValueKind != JsonValueKind.Null;
        bool originalDeclared = false;
        JsonElement original = default;
        JsonElement driver = default;
        if (report.TryGetProperty("initialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.TryGetProperty("driver", out driver))
        {
            originalDeclared = driver.TryGetProperty("counter_cycle", out original) && original.ValueKind != JsonValueKind.Null;
            if (originalDeclared && preparation.TryGetProperty("prelude", out JsonElement prelude) && prelude.ValueKind != JsonValueKind.Null)
            {
                throw new InvalidDataException("Counter evidence cannot use a preparation prelude before its ordinary initial admission.");
            }
            if (originalDeclared && (!JsonElement.DeepEquals(input, driver.GetProperty("input")) || Number(driver, "expected_effect") != effect || OptionalNumber(driver, "expected_native_action") != action || OptionalNumber(driver, "expected_native_command") != command))
            {
                throw new InvalidDataException("Counter control input or focal identity differs from the initialized request.");
            }
        }
        if (declared != originalDeclared || declared && !JsonElement.DeepEquals(value, original))
        {
            throw new InvalidDataException("Counter declaration differs from the initialized request.");
        }
        if (!declared)
        {
            return null;
        }
        DiagnosticCounterCycleDeclaration declaration = value.Deserialize<DiagnosticCounterCycleDeclaration>(Json) ?? throw new InvalidDataException("Counter declaration is empty.");
        bool HasOwner(string name) => driver.TryGetProperty(name, out JsonElement owner) && owner.ValueKind != JsonValueKind.Null;
        ushort[] effects = report.GetProperty("supporting_effects").Deserialize<ushort[]>(Json) ?? throw new InvalidDataException("Counter supporting effects are absent.");
        BattleDiagnosticSupportingInvocation[] invocations = report.GetProperty("supporting_invocations").Deserialize<BattleDiagnosticSupportingInvocation[]>(Json) ?? throw new InvalidDataException("Counter supporting invocations are absent.");
        declaration.ValidateComposition(input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Counter control input is absent."), checked((ushort)effect), checked((int)action), command is null ? null : checked((byte)command), HasOwner("shot_cycle"), HasOwner("enemy_ability"), HasOwner("limit_selection"), effects, invocations);
        return declaration;
    }

    private static BattleDiagnosticSupportingInvocation[] ReadSupportingInvocations(JsonElement report, string actor, long effect)
    {
        if (!report.TryGetProperty("supporting_invocations", out JsonElement declared))
        {
            return [];
        }
        BattleDiagnosticSupportingInvocation[] invocations = declared.Deserialize<BattleDiagnosticSupportingInvocation[]>(Json) ?? throw new InvalidDataException("Supporting invocations must be an explicit array.");
        if (invocations.Length > 32 || invocations.Any(invocation => invocation is null) || invocations.Distinct().Count() != invocations.Length)
        {
            throw new InvalidDataException("Supporting invocations must be distinct bounded declarations.");
        }
        foreach (BattleDiagnosticSupportingInvocation invocation in invocations)
        {
            invocation.Validate();
            if (invocation.ActorId == actor || invocation.EffectId == effect)
            {
                throw new InvalidDataException("Supporting invocations cannot replace the focal actor or effect.");
            }
        }
        if (report.TryGetProperty("initialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.TryGetProperty("driver", out JsonElement driver))
        {
            bool selected = driver.TryGetProperty("supporting_invocations", out JsonElement original);
            if (selected ? !JsonElement.DeepEquals(original, declared) : invocations.Length != 0)
            {
                throw new InvalidDataException("Supporting invocations differ from the initialized recipe.");
            }
        }
        return invocations;
    }

    private static BattleDiagnosticInvocationRelease ValidateFreshInvocationRelease(JsonElement report, JsonElement execution, BattleDiagnosticEffectDispatch[] dispatches, long startCallback, long endCallback, long endFrame)
    {
        BattleDiagnosticInvocationRelease release = execution.GetProperty("invocation_release").Deserialize<BattleDiagnosticInvocationRelease>(Json) ?? throw new InvalidDataException("Fresh entry requires its actual native invocation release.");
        BattleDiagnosticEffectDispatch focal = dispatches.Single(value => value.Serial == release.Dispatch.Serial);
        TelemetryOwnerState end = execution.GetProperty("end_owners").Deserialize<TelemetryOwnerState>(Json) ?? throw new InvalidDataException("Fresh release has no retained end owner.");
        DiagnosticInvocationReleaseValidation.Validate(release, focal, Guid.Parse(end.RetainedMemoryOwnerId));
        JsonElement tail = execution.GetProperty("post_release_tail");
        long first = Number(tail, "first_presented_frame");
        long last = Number(tail, "last_presented_frame");
        long firstTicks = Number(tail, "first_timestamp");
        long lastTicks = Number(tail, "last_timestamp");
        long count = Number(tail, "presented_frames");
        long requiredTail = Number(report.GetProperty("initialization").GetProperty("preparation").GetProperty("driver"), "tail_frames");
        if (release.Callback != endCallback || release.Callback <= startCallback || release.Timestamp != Number(execution, "effect_completed_ticks") || first != endFrame + 1 || requiredTail < 2 || count < requiredTail || last - first + 1 != count || Number(execution, "tail_presented_frames") != count || firstTicks <= release.Timestamp || lastTicks <= firstTicks)
        {
            throw new InvalidDataException("Fresh release and separate actual presentation tail do not match their focal execution boundary.");
        }
        if (execution.GetProperty("events").EnumerateArray().Any(value => Number(value, "frame") > release.SimulationFrame) || execution.GetProperty("resolutions").EnumerateArray().Any(value => Number(value, "callback") > release.Callback))
        {
            throw new InvalidDataException("Fresh focal window contains gameplay after its sealed native release.");
        }
        return release;
    }

    private static bool HasSupportingInvocation(JsonElement execution, JsonElement dispatch, BattleDiagnosticSupportingInvocation[] declared)
    {
        string actor = Text(dispatch, "actor_id");
        BattleDiagnosticSupportingInvocation[] matched = declared.Where(invocation => invocation.ActorId == actor && invocation.NativeCommand == OptionalNumber(dispatch, "native_command") && invocation.ActionId == Number(dispatch, "action_id") && invocation.EffectId == Number(dispatch, "requested_effect") && invocation.CallbackStage == OptionalNumber(dispatch, "native_ai_callback_stage")).ToArray();
        if (matched.Length != 1 || Text(dispatch, "ability") != "EnemyAbility" || !dispatch.TryGetProperty("native_ai_script_sha256", out JsonElement observed) || observed.ValueKind != JsonValueKind.String || !string.Equals(observed.GetString(), matched[0].ScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        JsonElement[] before = execution.GetProperty("before").EnumerateArray().Where(state => Text(state, "id") == actor).ToArray();
        JsonElement[] after = execution.GetProperty("after").EnumerateArray().Where(state => Text(state, "id") == actor).ToArray();
        if (before.Length != 1 || after.Length != 1 || OptionalNumber(before[0], "native_index") is not (>= 3 and <= 6) || OptionalNumber(before[0], "native_index") != OptionalNumber(after[0], "native_index") || OptionalNumber(before[0], "model_id") is not (>= 0) || OptionalNumber(before[0], "model_id") != OptionalNumber(after[0], "model_id"))
        {
            return false;
        }
        long? physical = OptionalNumber(before[0], "native_index");
        if (execution.GetProperty("before").EnumerateArray().Count(state => OptionalNumber(state, "native_index") == physical) != 1 || execution.GetProperty("after").EnumerateArray().Count(state => OptionalNumber(state, "native_index") == physical) != 1)
        {
            return false;
        }
        return before.Concat(after).All(state => state.TryGetProperty("native_ai_script_sha256", out JsonElement scripts) && scripts.ValueKind == JsonValueKind.Array && scripts.GetArrayLength() > matched[0].CallbackStage && scripts[matched[0].CallbackStage].ValueKind == JsonValueKind.String && string.Equals(scripts[matched[0].CallbackStage].GetString(), matched[0].ScriptSha256, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateTerminalHolds(JsonElement report, JsonElement executions, List<string> errors, List<string> diagnostics, JsonElement? repairOverride = null)
    {
        bool declared = report.TryGetProperty("declared_terminal_hold", out JsonElement declaration) && declaration.ValueKind != JsonValueKind.Null;
        bool hasReceipts = report.TryGetProperty("terminal_holds", out JsonElement receipts) && receipts.ValueKind == JsonValueKind.Array && receipts.GetArrayLength() != 0;
        if (report.TryGetProperty("initialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.TryGetProperty("driver", out JsonElement driver))
        {
            bool initialized = driver.TryGetProperty("terminal_hold", out JsonElement original) && original.ValueKind != JsonValueKind.Null;
            if (initialized != declared || initialized && !JsonElement.DeepEquals(original, declaration))
            {
                errors.Add("TERMINAL_HOLD_DECLARATION_MISMATCH");
            }
        }
        if (!declared)
        {
            if (hasReceipts)
            {
                errors.Add("TERMINAL_HOLD_UNDECLARED");
            }
            return;
        }
        BattleDiagnosticTerminalHoldDeclaration config = declaration.Deserialize<BattleDiagnosticTerminalHoldDeclaration>(Json) ?? throw new InvalidDataException("Missing terminal hold declaration.");
        config.Validate();
        bool hasRepairs = report.TryGetProperty("repairs", out JsonElement reportRepairs);
        JsonElement repairs = repairOverride ?? reportRepairs;
        if (!hasReceipts || receipts.GetArrayLength() is < 1 or > 4 || executions.GetArrayLength() != 2 || !hasRepairs || repairs.GetArrayLength() != 1 || !report.TryGetProperty("declared_repairs", out JsonElement declaredRepairs) || declaredRepairs.ValueKind != JsonValueKind.Array || declaredRepairs.GetArrayLength() != config.PartyActorIds.Length || !declaredRepairs.EnumerateArray().Select(repair => Text(repair, "actor_id")).ToHashSet(StringComparer.Ordinal).SetEquals(config.PartyActorIds))
        {
            errors.Add("TERMINAL_HOLD_EVIDENCE_INVALID");
            return;
        }
        BattleDiagnosticTerminalHoldReceipt[] proof = receipts.Deserialize<BattleDiagnosticTerminalHoldReceipt[]>(Json)!;
        string path = string.Join(",", proof.Select(item => item.Kind));
        int[] sequences;
        int[] executionIndices;
        switch (path)
        {
            case "released":
                sequences = [0];
                executionIndices = [1];
                break;
            case "held,released":
                sequences = [1, 1];
                executionIndices = [1, 1];
                break;
            case "held,rearmed,released":
                sequences = [1, 1, 1];
                executionIndices = [0, 1, 1];
                break;
            case "held,rearmed,held,released":
                sequences = [1, 1, 2, 2];
                executionIndices = [0, 1, 1, 1];
                break;
            default:
                errors.Add("TERMINAL_HOLD_EVIDENCE_INVALID");
                return;
        }
        Guid id = proof[0].HoldId;
        string lifecycle = Text(executions[0].GetProperty("start_owners"), "retained_memory_owner_id");
        long previous = -1;
        long previousFrame = -1;
        long? heldCallback = null;
        for (int index = 0; index < proof.Length; index++)
        {
            BattleDiagnosticTerminalHoldReceipt item = proof[index];
            if (id == Guid.Empty || item.HoldId != id || item.LifecycleId.ToString() != lifecycle || item.Sequence != sequences[index] || item.Candidate != BattleOutcome.Defeat || item.NativeResult is > 0 || item.Callback < previous || item.Frame < previousFrame || item.Frame < 0 || item.RestoreCount != 0 || item.ResetCount != 0 || item.Party is null || item.Random is null || item.Party.Any(actor => actor is null) || !item.Party.Select(actor => actor.Id).SequenceEqual(config.PartyActorIds))
            {
                errors.Add("TERMINAL_HOLD_OWNER_CONTINUITY_BROKEN");
                return;
            }
            JsonElement execution = executions[executionIndices[index]];
            Dictionary<string, JsonElement> before = execution.GetProperty("before").EnumerateArray().ToDictionary(actor => Text(actor, "id"), StringComparer.Ordinal);
            Dictionary<string, JsonElement> after = execution.GetProperty("after").EnumerateArray().ToDictionary(actor => Text(actor, "id"), StringComparer.Ordinal);
            if (item.Party.Any(actor => actor.NativeIndex is not int native || native is < 0 or >= 3 || !HasPhysicalIdentity(before, actor.Id, checked((byte)native)) || !HasPhysicalIdentity(after, actor.Id, checked((byte)native)) || (actor.Statuses1 & 0x80010000) != 0 || actor.CurrentHp < 0 || actor.CurrentHp > actor.MaximumHp) || item.Kind == "held" && item.Party.Any(actor => (actor.Statuses0 & 5) == 0) || item.Kind is "rearmed" or "released" && !item.Drained)
            {
                errors.Add("TERMINAL_HOLD_PARTY_PROVENANCE_INVALID");
            }
            if (item.Kind == "held")
            {
                if (item.Party.Any(actor => !after.TryGetValue(actor.Id, out JsonElement state) || (Number(state, "statuses0") & 5) == 0))
                {
                    errors.Add("TERMINAL_HOLD_DEFEAT_NOT_OBSERVED");
                }
                if (item.Callback < Number(execution, "start_callback") || item.Callback > Number(execution, "end_callback"))
                {
                    errors.Add("TERMINAL_HOLD_TIMING_OR_REPAIR_INVALID");
                }
                heldCallback = item.Callback;
            }
            else
            {
                if (heldCallback is long admission && item.Callback - admission > config.MaximumCallbacks)
                {
                    errors.Add("TERMINAL_HOLD_TIMING_OR_REPAIR_INVALID");
                }
                heldCallback = null;
                if (item.Kind == "rearmed")
                {
                    if (item.Callback != Number(repairs[0], "callback_after") || item.Frame != Number(repairs[0], "frame") || item.Callback < Number(executions[0], "end_callback") || item.Callback > Number(executions[1], "start_callback") || !JsonElement.DeepEquals(receipts[index].GetProperty("random"), repairs[0].GetProperty("random_after")))
                    {
                        errors.Add("TERMINAL_HOLD_TIMING_OR_REPAIR_INVALID");
                    }
                    if (item.Party.Any(actor => !before.TryGetValue(actor.Id, out JsonElement state) || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actor, RecordJson.Options), state) || (actor.Statuses0 & 5) != 0))
                    {
                        errors.Add("TERMINAL_HOLD_REARM_LINEAGE_INVALID");
                    }
                }
                else if (item.Callback < Number(executions[1], "end_callback") || item.Party.Any(actor => !after.TryGetValue(actor.Id, out JsonElement state) || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actor, RecordJson.Options), state)))
                {
                    errors.Add("TERMINAL_HOLD_RELEASE_LINEAGE_INVALID");
                }
            }
            previous = item.Callback;
            previousFrame = item.Frame;
        }
        if (path == "released")
        {
            diagnostics.Add("DEFEAT_HOLD_NOT_TRIGGERED");
        }
        diagnostics.Add("CONTROLLED_DEFEAT_HOLD_REQUIRES_SEPARATE_NATURAL_TERMINAL_CHECK");
    }

    private static void ValidateCounterRepairs(JsonElement report, JsonElement executions, DiagnosticCounterCycleReceipt[] cycles)
    {
        JsonElement[] declared = report.GetProperty("declared_repairs").EnumerateArray().ToArray();
        JsonElement driver = report.GetProperty("initialization").GetProperty("preparation").GetProperty("driver");
        if (!JsonElement.DeepEquals(report.GetProperty("declared_repairs"), driver.GetProperty("repairs")))
        {
            throw new InvalidDataException("Counter repair policy differs from the initialized request.");
        }
        BattleDiagnosticRepairReceipt[] typed = cycles.SelectMany(cycle => cycle.Attempts).Where(attempt => attempt.PreparationRepair is not null).Select(attempt => attempt.PreparationRepair!).ToArray();
        if (!JsonElement.DeepEquals(report.GetProperty("repairs"), JsonSerializer.SerializeToElement(typed, Json)))
        {
            throw new InvalidDataException("Counter repair ledger contains an unbound or omitted attempt preparation.");
        }
        string lifecycle = Text(executions[0].GetProperty("start_owners"), "retained_memory_owner_id");
        foreach (BattleDiagnosticRepairReceipt repair in typed)
        {
            DiagnosticCounterCycleValidation.ValidatePreparationRepair(repair);
            if (repair.LifecycleId.ToString() != lifecycle || repair.Changes.Length != declared.Length || repair.Changes.Any(change => !declared.Any(value => JsonElement.DeepEquals(value, JsonSerializer.SerializeToElement(change.Request, Json)))))
            {
                throw new InvalidDataException("Counter preparation changed the declared player policy or retained battle owner.");
            }
            foreach (BattleDiagnosticRepairChange change in repair.Changes)
            {
                foreach (JsonElement execution in executions.EnumerateArray())
                {
                    foreach (string boundary in new[] { "before", "after" })
                    {
                        JsonElement[] actual = execution.GetProperty(boundary).EnumerateArray().Where(actor => Text(actor, "id") == change.Request.ActorId).ToArray();
                        if (actual.Length != 1 || OptionalNumber(actual[0], "native_index") != change.Before.NativeIndex || OptionalNumber(actual[0], "model_id") != change.Before.ModelId)
                        {
                            throw new InvalidDataException("Counter preparation recipient differs from its actual physical party owner.");
                        }
                    }
                }
            }
        }
        if (cycles[1].Attempts[0].PreparationRepair is { } intercycle)
        {
            foreach (var change in intercycle.Changes)
            {
                JsonElement[] before = executions[0].GetProperty("after").EnumerateArray().Where(actor => Text(actor, "id") == change.Request.ActorId).ToArray();
                JsonElement[] after = executions[1].GetProperty("before").EnumerateArray().Where(actor => Text(actor, "id") == change.Request.ActorId).ToArray();
                if (before.Length != 1 || after.Length != 1 || !JsonElement.DeepEquals(before[0], JsonSerializer.SerializeToElement(change.Before, Json)) || !JsonElement.DeepEquals(after[0], JsonSerializer.SerializeToElement(change.After, Json)))
                {
                    throw new InvalidDataException("Counter intercycle preparation does not join the actual native cleanup/readiness states.");
                }
            }
        }
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
        if (callback != Number(receipt, "callback_after") || callback < Number(executions[0], "end_callback") || callback > Number(executions[1], "start_callback") || Number(receipt, "frame") < 0 || Number(receipt, "restore_count") != 0 || Number(receipt, "reset_count") != 0 || Text(receipt, "lifecycle_id") != Text(executions[0].GetProperty("start_owners"), "retained_memory_owner_id") || !JsonElement.DeepEquals(receipt.GetProperty("random_before"), receipt.GetProperty("random_after")) || !JsonElement.DeepEquals(receipt.GetProperty("items_before"), receipt.GetProperty("items_after")))
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
            bool enemy = before.TryGetProperty("native_index", out JsonElement nativeIndex) && nativeIndex.ValueKind == JsonValueKind.Number && nativeIndex.TryGetInt32(out int physicalIndex) && physicalIndex >= 3 || before.TryGetProperty("native_ai_script_sha256", out JsonElement ownerScripts) && ownerScripts.ValueKind == JsonValueKind.Array;
            if (enemy)
            {
                bool sameOwner = nativeIndex.ValueKind == JsonValueKind.Number && nativeIndex.TryGetInt32(out int native) && native is >= 3 and <= 6 && before.TryGetProperty("model_id", out JsonElement model) && model.ValueKind == JsonValueKind.Number && model.TryGetInt32(out int modelId) && modelId >= 0 && before.TryGetProperty("native_ai_script_sha256", out JsonElement scripts) && scripts.ValueKind == JsonValueKind.Array && scripts.GetArrayLength() != 0 && scripts.EnumerateArray().All(script => script.ValueKind == JsonValueKind.String && script.GetString() is string hash && hash.Length == 64 && hash.All(Uri.IsHexDigit));
                foreach (string property in new[] { "native_index", "model_id", "native_ai_script_sha256" })
                {
                    sameOwner &= before.TryGetProperty(property, out JsonElement originalOwner) && after.TryGetProperty(property, out JsonElement repairedOwner) && JsonElement.DeepEquals(originalOwner, repairedOwner);
                }
                if (!sameOwner)
                {
                    errors.Add("REPAIR_OWNER_CONTINUITY_BROKEN");
                }
                if (repair.Revive || (repair.RemoveStatuses0 & 1) != 0 || repair.HitPoints != Number(before, "current_hp") || Number(after, "current_hp") != Number(before, "current_hp"))
                {
                    errors.Add("REPAIR_STATUS_MUTATION_INVALID");
                }
            }
            long expectedSaved = enemy ? beforeSaved & ~repair.RemoveStatuses0 : (beforeSaved & ~(repair.RemoveStatuses0 | (repair.Revive ? 1 : 0))) & 0xFCFF;
            // The ordinary HP commit recomputes saved low-HP bits after the declared removals.
            if (!enemy && repair.HitPoints < (maximum >> 1))
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

    // Native timed Renzokuken releases through its guarded47DE70 descriptor, outside the generic ActiveAction mirror.
    private static bool ValidateNativeRenzokukenCompletion(JsonElement report, JsonElement execution, int ordinal, JsonElement input, long effect, long nativeAction, long? nativeCommand)
    {
        bool selected = Text(input, "ability") == "LimitBreak" && Number(input, "action_id") == 5 << 16;
        if (!selected && nativeCommand != 250)
        {
            return false;
        }
        JsonElement preparation = report.GetProperty("initialization").GetProperty("preparation");
        JsonElement original = preparation.GetProperty("driver");
        BattleInput submitted = input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Native Renzokuken completion lacks its real semantic input.");
        submitted.Validate();
        if (!selected || submitted.Kind != BattleInputKind.Action || submitted.DrawCast || !submitted.MagicTargets.IsEmpty || effect is < 162 or > 165 || nativeCommand != 250 || nativeAction != 65535 || !JsonElement.DeepEquals(input, original.GetProperty("input")) || Number(original, "expected_effect") != effect || OptionalNumber(original, "expected_native_command") != 250 || OptionalNumber(original, "expected_native_action") != 65535 || preparation.TryGetProperty("prelude", out JsonElement prelude) && prelude.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException("Native Renzokuken completion requires its exact initialized natural command5/250 finisher role.");
        }
        foreach (string bypass in new[] { "shot_cycle", "counter_cycle", "enemy_ability" })
        {
            if (original.TryGetProperty(bypass, out JsonElement value) && value.ValueKind != JsonValueKind.Null)
            {
                throw new InvalidDataException("Native Renzokuken completion cannot borrow another diagnostic coordinator.");
            }
        }
        if (!report.GetProperty("supporting_effects").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 340 }) || !JsonElement.DeepEquals(report.GetProperty("supporting_effects"), original.GetProperty("supporting_effects")) || report.GetProperty("supporting_invocations").GetArrayLength() != 0 || original.GetProperty("supporting_invocations").GetArrayLength() != 0)
        {
            throw new InvalidDataException("Native Renzokuken completion requires its sole real opening effect340 without supporting invocation bypasses.");
        }
        BattleDiagnosticLimitSelection policy = original.GetProperty("limit_selection").Deserialize<BattleDiagnosticLimitSelection>(Json) ?? throw new InvalidDataException("Native Renzokuken completion lacks ordinary Limit selection policy.");
        policy.Validate();
        BattleDiagnosticLimitSelectionReceipt[] selections = report.GetProperty("limit_selections").Deserialize<BattleDiagnosticLimitSelectionReceipt[]>(Json) ?? throw new InvalidDataException("Native Renzokuken selections are absent.");
        if (selections.Length != 2 || ordinal is < 1 or > 2)
        {
            throw new InvalidDataException("Native Renzokuken completion requires two independent real selections.");
        }
        var selection = selections[ordinal - 1];
        if (selection.ActorId != submitted.ActorId || selection.CommandOwner != 0 || selection.Command != 5 || !selection.Accepted || selection.CrisisSelections.IsDefaultOrEmpty || selection.CrisisSelections.Length > policy.MaximumSelections || selection.CrisisSelections.Any(value => value > 4) || selection.CrisisSelections[^1] < policy.MinimumCrisisLevel || selection.CrisisSelections[^1] > policy.MaximumCrisisLevel || selection.CrisisSelections.Take(selection.CrisisSelections.Length - 1).Any(value => value >= policy.MinimumCrisisLevel && value <= policy.MaximumCrisisLevel) || selection.RandomBefore is null || selection.RandomAfter is null)
        {
            throw new InvalidDataException("Native Renzokuken completion lost its accepted ordinary Squall Limit selection.");
        }
        selection.RandomBefore.Validate();
        selection.RandomAfter.Validate();
        string actor = submitted.ActorId!;
        string target = submitted.TargetId!;
        JsonElement initializedTarget = report.GetProperty("initialization").GetProperty("target").GetProperty("target");
        JsonElement requestedTarget = preparation.GetProperty("target");
        if (Text(initializedTarget, "actor_id") != target || Text(requestedTarget, "actor_id") != target || OptionalNumber(initializedTarget, "physical_index") is not (>= 3 and < 7) || OptionalNumber(requestedTarget, "physical_index") != Number(initializedTarget, "physical_index") || !Flag(initializedTarget, "alive") || !Flag(initializedTarget, "statistics_ready"))
        {
            throw new InvalidDataException("Native Renzokuken completion lost its actually initialized target expectation.");
        }
        byte initializedTargetIndex = checked((byte)Number(initializedTarget, "physical_index"));
        byte? actorIndex = null;
        byte? targetIndex = null;
        foreach (string boundary in new[] { "before", "after" })
        {
            JsonElement[] states = execution.GetProperty(boundary).EnumerateArray().ToArray();
            JsonElement[] actors = states.Where(state => Text(state, "id") == actor).ToArray();
            JsonElement[] targets = states.Where(state => Text(state, "id") == target).ToArray();
            if (actors.Length != 1 || targets.Length != 1 || OptionalNumber(actors[0], "native_index") is not (>= 0 and < 3) || OptionalNumber(actors[0], "model_id") != 0 || OptionalNumber(targets[0], "native_index") is not (>= 3 and < 7))
            {
                throw new InvalidDataException("Native Renzokuken completion requires its actual physical Squall/target owners.");
            }
            byte currentActor = checked((byte)Number(actors[0], "native_index"));
            byte currentTarget = checked((byte)Number(targets[0], "native_index"));
            if (currentTarget != initializedTargetIndex || actorIndex is not null && actorIndex != currentActor || targetIndex is not null && targetIndex != currentTarget)
            {
                throw new InvalidDataException("Native Renzokuken completion replaced its physical actors.");
            }
            actorIndex = currentActor;
            targetIndex = currentTarget;
        }
        if (ordinal == 2)
        {
            JsonElement[] firstActors = report.GetProperty("executions")[0].GetProperty("before").EnumerateArray().Where(state => Text(state, "id") == actor).ToArray();
            if (firstActors.Length != 1 || OptionalNumber(firstActors[0], "native_index") != actorIndex)
            {
                throw new InvalidDataException("Native Renzokuken cycles replaced their physical Squall owner.");
            }
        }
        BattleDiagnosticEffectDispatch[] dispatches = execution.GetProperty("dispatches").Deserialize<BattleDiagnosticEffectDispatch[]>(Json) ?? throw new InvalidDataException("Native Renzokuken dispatches are absent.");
        if (dispatches.Length != 2)
        {
            throw new InvalidDataException("Native Renzokuken completion requires exactly its opening and focal dispatch.");
        }
        JsonElement[] priorDispatches = ordinal == 2 ? report.GetProperty("executions")[0].GetProperty("dispatches").EnumerateArray().ToArray() : [];
        long previousSerial = ordinal == 2 ? Number(priorDispatches[^1], "serial") : 0;
        long previousGeneration = ordinal == 2 ? Number(priorDispatches[^1], "native_header_generation") : 0;
        long previousCallback = Number(execution, "start_callback");
        for (int index = 0; index < dispatches.Length; index++)
        {
            var dispatch = dispatches[index];
            if (dispatch.ActorId != actor || dispatch.Ability != BattleAbility.LimitBreak || dispatch.NativeActorIndex != actorIndex || dispatch.NativeCommand != (index == 0 ? 5 : 250) || dispatch.ActionId != (index == 0 ? 65534 : 65535) || dispatch.RequestedEffect != (index == 0 ? 340 : effect) || dispatch.ExecutedEffect != dispatch.RequestedEffect || dispatch.Reflection is not null || dispatch.NativeHeaderIndex is not (>= 0 and < 32) || dispatch.NativeHeaderGeneration is not long generation || generation <= previousGeneration || dispatch.Serial <= previousSerial || dispatch.Callback < previousCallback || dispatch.Callback > Number(execution, "end_callback") || dispatch.NativeTargetMask != 1 << targetIndex!.Value)
            {
                throw new InvalidDataException("Native Renzokuken completion lost its ordered opening/focal header and target lineage.");
            }
            previousSerial = dispatch.Serial;
            previousGeneration = generation;
            previousCallback = dispatch.Callback;
        }
        BattleEvent[] events = execution.GetProperty("events").Deserialize<BattleEvent[]>(Json) ?? throw new InvalidDataException("Native Renzokuken event ledger is absent.");
        BattleEvent[] starts = events.Where(entry => entry.Kind == "Renzokuken").ToArray();
        BattleEvent[] triggers = events.Where(entry => entry.Kind == "GunbladeTrigger").ToArray();
        long[] allCallbacks = report.GetProperty("gunblade_triggers").EnumerateArray().Select(value => value.GetInt64()).ToArray();
        if (allCallbacks.Any(value => value < 0) || allCallbacks.Zip(allCallbacks.Skip(1)).Any(pair => pair.First >= pair.Second))
        {
            throw new InvalidDataException("Native Renzokuken trigger callbacks must retain their actual ordered identities.");
        }
        long[] callbacks = allCallbacks.Where(value => value >= dispatches[0].Callback && value <= dispatches[1].Callback).ToArray();
        if (ordinal == 2 && allCallbacks.Any(value => !report.GetProperty("executions").EnumerateArray().Any(cycle => value >= Number(cycle.GetProperty("dispatches")[0], "callback") && value <= Number(cycle.GetProperty("dispatches")[1], "callback"))))
        {
            throw new InvalidDataException("Native Renzokuken trigger evidence falls outside its actual opening/focal cycles.");
        }
        if (starts.Length != 1 || starts[0].ActorId != actor || starts[0].TargetId != target || starts[0].Amount <= 0 || triggers.Length != starts[0].Amount || triggers.Any(entry => entry.ActorId != actor || entry.TargetId != target) || callbacks.Length != triggers.Length || callbacks.Zip(callbacks.Skip(1)).Any(pair => pair.First >= pair.Second) || !Flag(execution, "completed") || !Flag(execution, "cleanup_completed") || Number(execution, "dropped_events") != 0 || Number(execution.GetProperty("frames"), "dropped_samples") != 0 || !Flag(execution.GetProperty("frames"), "valid") || execution.GetProperty("integrity_error").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException("Native Renzokuken completion requires its complete authored trigger count and full native owner drain.");
        }
        foreach (string boundary in new[] { "start_owners", "end_owners" })
        {
            JsonElement owner = execution.GetProperty(boundary);
            if (Number(owner, "constructor_count") != 1 || Number(owner, "restore_count") != 0 || Number(owner, "reset_count") != 0)
            {
                throw new InvalidDataException("Native Renzokuken completion cannot restore or replace its native action owners.");
            }
        }
        return true;
    }

    private static void ValidateSemantics(JsonElement execution, string actor, string submittedTarget, string ability, long action, long nativeAction, long effect, long startCallback, long endCallback, List<string> errors, BattleDiagnosticRepairReceipt[]? counterRepairs = null, bool nativeRenzCompleted = false)
    {
        Dictionary<string, JsonElement> beforeStates = execution.GetProperty("before").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, JsonElement> afterStates = execution.GetProperty("after").EnumerateArray().ToDictionary(state => Text(state, "id"), StringComparer.Ordinal);
        Dictionary<string, long> observedHp = beforeStates.ToDictionary(pair => pair.Key, pair => Number(pair.Value, "current_hp"), StringComparer.Ordinal);
        JsonElement[] resolutions = execution.GetProperty("resolutions").EnumerateArray().ToArray();
        bool completed = nativeRenzCompleted || execution.GetProperty("events").EnumerateArray().Any(value => Text(value, "kind") == "ActionComplete" && Text(value, "actor_id") == actor);
        if (!completed)
        {
            errors.Add("SEMANTIC_RESOLUTION_UNVERIFIED");
        }
        Dictionary<string, long> deltas = [];
        BattleDiagnosticRepairReceipt[] pendingRepairs = counterRepairs ?? [];
        int repairIndex = 0;
        HashSet<(Guid Lifecycle, long Frame, long Callback, string Actor)> repairProjections = [];
        void ApplyRepairsBefore(long callback, bool includeBoundary)
        {
            while (repairIndex < pendingRepairs.Length && (pendingRepairs[repairIndex].CallbackBefore < callback || includeBoundary && pendingRepairs[repairIndex].CallbackBefore == callback))
            {
                BattleDiagnosticRepairReceipt repair = pendingRepairs[repairIndex++];
                foreach (BattleDiagnosticRepairChange change in repair.Changes)
                {
                    if (!observedHp.TryGetValue(change.Before.Id, out long previous) || previous != change.Before.CurrentHp)
                    {
                        errors.Add("COUNTER_REPAIR_HP_LINEAGE_INVALID");
                    }
                    observedHp[change.Before.Id] = change.After.CurrentHp;
                    deltas[change.Before.Id] = checked(deltas.GetValueOrDefault(change.Before.Id) + change.After.CurrentHp - change.Before.CurrentHp);
                }
            }
        }
        foreach (JsonElement resolution in resolutions)
        {
            long actual = Number(resolution, "actual_amount");
            long resolved = Number(resolution, "resolved_amount");
            long before = Number(resolution, "before_hp");
            long after = Number(resolution, "after_hp");
            long callback = Number(resolution, "callback");
            ApplyRepairsBefore(callback, false);
            string target = Text(resolution, "actual_target_id");
            string kind = Text(resolution, "kind");
            bool periodic = resolution.TryGetProperty("periodic_status", out JsonElement receipt) && receipt.ValueKind != JsonValueKind.Null;
            bool nativeWord = resolution.TryGetProperty("native_hp_word", out JsonElement hpReceipt) && hpReceipt.ValueKind != JsonValueKind.Null;
            bool nativeContact = resolution.TryGetProperty("native_contact", out JsonElement contactReceipt) && contactReceipt.ValueKind != JsonValueKind.Null;
            bool nativeRemoval = resolution.TryGetProperty("native_status_removal", out JsonElement removalReceipt) && removalReceipt.ValueKind != JsonValueKind.Null;
            if (resolution.TryGetProperty("diagnostic_repair", out JsonElement repairProjection) && repairProjection.ValueKind != JsonValueKind.Null)
            {
                if (counterRepairs is null)
                {
                    throw new InvalidDataException("An in-execution diagnostic repair projection requires its typed counter preparation role.");
                }
                _ = Number(resolution, "frame");
                if (resolution.GetProperty("actor_id").ValueKind != JsonValueKind.Null)
                {
                    throw new InvalidDataException("Diagnostic repair projection cannot invent a native action actor.");
                }
                BattleDiagnosticResolution typed = resolution.Deserialize<BattleDiagnosticResolution>(Json) ?? throw new InvalidDataException("Diagnostic repair projection is absent.");
                DiagnosticCounterCycleValidation.ValidateRepairProjection(typed, counterRepairs);
                BattleDiagnosticRepairProjection source = typed.DiagnosticRepair!;
                if (!repairProjections.Add((source.LifecycleId, source.Frame, source.Callback, source.Request.ActorId)))
                {
                    throw new InvalidDataException("Diagnostic repair projection duplicated its actual completed preparation owner.");
                }
                ApplyRepairsBefore(callback, true);
                continue;
            }
            bool validNativeRemoval = nativeRemoval && !periodic && !nativeWord && !nativeContact && HasNativeStatusRemovalProvenance(execution, resolution, beforeStates, afterStates);
            string originalTarget = Text(resolution, "original_target_id");
            if (!beforeStates.ContainsKey(originalTarget) && !afterStates.ContainsKey(originalTarget))
            {
                errors.Add("RESOLUTION_TARGET_UNKNOWN");
            }
            if (nativeContact && !HasGuardianContactProvenance(execution, resolution, beforeStates, afterStates, actor, submittedTarget, ability, action, nativeAction, effect))
            {
                errors.Add("NATIVE_CONTACT_PROVENANCE_INVALID");
            }
            if (nativeWord && (!HasNativeHpWordProvenance(resolution, beforeStates, afterStates) || periodic || nativeContact))
            {
                errors.Add("NATIVE_HP_WORD_PROVENANCE_INVALID");
            }
            if (periodic && (!HasPeriodicStatusProvenance(resolution, beforeStates, afterStates) || nativeWord || nativeContact))
            {
                errors.Add("PERIODIC_STATUS_PROVENANCE_INVALID");
            }
            if (nativeRemoval && !validNativeRemoval)
            {
                errors.Add("NATIVE_STATUS_REMOVAL_PROVENANCE_INVALID");
            }
            if (!periodic && !nativeWord && (resolution.GetProperty("actor_id").ValueKind != JsonValueKind.String || !beforeStates.ContainsKey(Text(resolution, "actor_id"))))
            {
                errors.Add("RESOLUTION_ACTOR_UNKNOWN");
            }
            long delta = after - before;
            bool healing = kind.Contains("heal", StringComparison.OrdinalIgnoreCase);
            bool damage = kind.Contains("damage", StringComparison.OrdinalIgnoreCase);
            if (callback < startCallback || callback > endCallback || string.IsNullOrWhiteSpace(Text(resolution, "hp_owner")) || string.IsNullOrWhiteSpace(Text(resolution, "original_target_id")) || !nativeWord && (actual < 0 || resolved < 0 || resolved < actual && !validNativeRemoval || before < 0 || after < 0 || !healing && !damage || delta != (healing ? actual : -actual)))
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
        foreach (BattleDiagnosticRepairReceipt repair in pendingRepairs)
        {
            foreach (BattleDiagnosticRepairChange change in repair.Changes)
            {
                if (!repairProjections.Contains((repair.LifecycleId, repair.Frame, repair.CallbackBefore, change.Request.ActorId)))
                {
                    throw new InvalidDataException("Counter preparation lacks its actual synchronous diagnostic HP projection.");
                }
            }
        }
        ApplyRepairsBefore(endCallback, true);
        if (repairIndex != pendingRepairs.Length)
        {
            errors.Add("COUNTER_REPAIR_EXECUTION_BOUNDARY_INVALID");
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

    private static bool HasNativeStatusRemovalProvenance(JsonElement execution, JsonElement resolution, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after)
    {
        BattleDiagnosticNativeStatusRemoval? proof = resolution.GetProperty("native_status_removal").Deserialize<BattleDiagnosticNativeStatusRemoval>(Json);
        string actor = Text(resolution, "actor_id");
        string recipient = Text(resolution, "actual_target_id");
        if (proof is null || proof.ProducerAddress != 0x494410 || (proof.ActionStatuses1 & 0x10000) == 0 || (proof.AppliedBattleStatuses & 0x10000) == 0 || Text(resolution, "kind") != "damage" || recipient != Text(resolution, "original_target_id") || !HasPhysicalIdentity(before, actor, proof.ActorIndex) || !HasPhysicalIdentity(after, actor, proof.ActorIndex) || !HasPhysicalIdentity(before, recipient, proof.RecipientIndex) || !HasPhysicalIdentity(after, recipient, proof.RecipientIndex) || Text(resolution, "hp_owner") != (proof.RecipientIndex < 3 ? "character" : "enemy") || Number(resolution, "resolved_amount") < 0 || Number(resolution, "actual_amount") != Number(resolution, "before_hp") || Number(resolution, "before_hp") <= 0 || Number(resolution, "after_hp") != 0 || Number(after[recipient], "current_hp") != 0 || (Number(after[recipient], "statuses0") & 1) == 0)
        {
            return false;
        }
        return execution.GetProperty("dispatches").EnumerateArray().Any(dispatch => Text(dispatch, "actor_id") == actor && OptionalNumber(dispatch, "native_actor_index") == proof.ActorIndex && OptionalNumber(dispatch, "native_command") == proof.NativeCommand && Number(dispatch, "action_id") == proof.NativeActionId && OptionalNumber(dispatch, "native_target_mask") is long mask && (mask & (1L << proof.RecipientIndex)) != 0 && Number(dispatch, "callback") >= Number(resolution, "callback"));
    }

    private static bool HasNativeHpWordProvenance(JsonElement resolution, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after)
    {
        BattleDiagnosticNativeHpWord? proof = resolution.GetProperty("native_hp_word").Deserialize<BattleDiagnosticNativeHpWord>(Json);
        if (proof is null || proof.ProducerAddress != 0x487DF0 || proof.Opcode != 60 || proof.ScriptIndex is < 0 or > 4 || proof.ScriptBytes.IsDefault || proof.ProgramCounter < 0 || proof.ProgramCounter > proof.ScriptBytes.Length - 3 || proof.Callback != Number(resolution, "callback") || Text(resolution, "kind") != "native_ai_hp_word" || resolution.GetProperty("actor_id").ValueKind != JsonValueKind.Null || proof.RecipientId != Text(resolution, "original_target_id") || proof.RecipientId != Text(resolution, "actual_target_id") || !HasPhysicalIdentity(before, proof.RecipientId, proof.RecipientIndex) || !HasPhysicalIdentity(after, proof.RecipientId, proof.RecipientIndex) || Text(resolution, "hp_owner") != (proof.RecipientIndex < 3 ? "character" : "enemy"))
        {
            return false;
        }
        string scriptHash = Convert.ToHexString(SHA256.HashData(proof.ScriptBytes.AsSpan()));
        foreach (JsonElement state in new[] { before[proof.RecipientId], after[proof.RecipientId] })
        {
            if (OptionalNumber(state, "model_id") != proof.ModelId || !state.TryGetProperty("native_ai_script_sha256", out JsonElement hashes) || hashes.ValueKind != JsonValueKind.Array || hashes.GetArrayLength() != 5 || hashes[proof.ScriptIndex].GetString() != scriptHash)
            {
                return false;
            }
        }
        byte[] script = proof.ScriptBytes.ToArray();
        int beforeHp = checked((int)Number(resolution, "before_hp"));
        int afterHp = checked((int)Number(resolution, "after_hp"));
        return script[proof.ProgramCounter] == proof.Opcode && BinaryPrimitives.ReadUInt16LittleEndian(script.AsSpan(proof.ProgramCounter + 1, 2)) == proof.Operand && proof.Amount == unchecked((short)proof.Operand) && proof.BeforeHpWord == unchecked((ushort)beforeHp) && proof.AfterHpWord == unchecked((ushort)afterHp) && afterHp == unchecked(beforeHp + proof.Amount) && Number(resolution, "resolved_amount") == proof.Amount && Number(resolution, "actual_amount") == proof.Amount;
    }

    private static bool HasPeriodicStatusProvenance(JsonElement resolution, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after)
    {
        BattleDiagnosticPeriodicStatus? proof = resolution.GetProperty("periodic_status").Deserialize<BattleDiagnosticPeriodicStatus>(Json);
        if (proof is null || proof.ProducerAddress != 0x483470 || resolution.GetProperty("actor_id").ValueKind != JsonValueKind.Null || proof.RecipientId != Text(resolution, "original_target_id") || proof.RecipientId != Text(resolution, "actual_target_id") || !HasPhysicalIdentity(before, proof.RecipientId, proof.RecipientIndex) || !HasPhysicalIdentity(after, proof.RecipientId, proof.RecipientIndex) || Text(resolution, "hp_owner") != (proof.RecipientIndex < 3 ? "character" : "enemy") || proof.Callback != Number(resolution, "callback") || proof.MaximumHp != Number(before[proof.RecipientId], "maximum_hp") || proof.ResolvedAmount != Number(resolution, "resolved_amount") || (proof.SavedStatuses & 5) != 0 || proof.TimerBefore == -1111)
        {
            return false;
        }
        string kind = Text(resolution, "kind");
        if (proof.StatusBit == 4)
        {
            int decrement = (proof.BattleStatuses & 2) != 0 ? 3 : 2;
            if ((proof.BattleStatuses & 4) != 0)
            {
                decrement = 1;
            }
            if ((proof.BattleStatuses & 9) != 0)
            {
                decrement = 0;
            }
            return (proof.BattleStatuses & 0x10) != 0 && proof.TimerBefore > 0 && decrement != 0 && proof.Decrement == decrement && proof.TimerAfter == unchecked((short)(proof.TimerBefore - decrement)) && proof.TimerBefore / decrement % (60 / decrement) == 0 && proof.ResolvedAmount == proof.MaximumHp / 20 && kind == ((proof.SavedStatuses & 0x40) != 0 ? "damage" : "healing") && Number(resolution, "actual_amount") == Math.Min(proof.ResolvedAmount, kind == "damage" ? Number(resolution, "before_hp") : proof.MaximumHp - Number(resolution, "before_hp"));
        }
        return proof.StatusBit == 10 && (proof.BattleStatuses & 0x400) != 0 && proof.TimerBefore <= 0 && proof.Decrement == 0 && proof.TimerAfter == -1111 && kind == "damage" && proof.ResolvedAmount == Number(resolution, "before_hp") && Number(resolution, "after_hp") == 0 && Number(resolution, "actual_amount") == proof.ResolvedAmount;
    }

    private static bool HasGuardianContactProvenance(JsonElement execution, JsonElement resolution, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after, string actor, string requestedTarget, string ability, long action, long nativeAction, long effect)
    {
        if (ability != "GuardianForce" || nativeAction is < 64 or > 79 || action != nativeAction - 64 || !execution.TryGetProperty("dispatches", out JsonElement dispatches) || !resolution.TryGetProperty("native_contact", out JsonElement contact) || contact.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        BattleDiagnosticNativeContact? proof = contact.Deserialize<BattleDiagnosticNativeContact>(Json);
        if (proof is null || proof.ProducerAddress != 0x4850A0 || proof.ActorId != actor || proof.SubmittedTargetId != requestedTarget || proof.RecipientId != Text(resolution, "original_target_id") || proof.HeaderIndex != 0 || proof.HeaderPayload.IsDefault || proof.HeaderPayload.Length != 20 || proof.ContactPayload.IsDefault || proof.ContactPayload.Length != 24 || proof.FirstContactIndex is not int first || first < 0)
        {
            return false;
        }
        byte[] header = proof.HeaderPayload.ToArray();
        const uint requiredHeaderBytes = (1u << 0) | (1u << 1) | (1u << 4) | (1u << 5) | (0xFu << 8) | (1u << 16);
        if ((proof.HeaderUnknownByteMask & ~0xFFFFFu) != 0 || (proof.HeaderUnknownByteMask & requiredHeaderBytes) != 0 || header[0] != proof.ActorIndex || header[1] != 254 || (header[4] | header[5] << 8) != nativeAction || (long)first + header[16] > NativeBattleActionPacketsState.ContactCapacity || proof.RetainedContactIndex < first || (long)proof.RetainedContactIndex >= (long)first + header[16] || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) != NativeBattleActionPacketsState.ContactBase + 24u * (uint)first)
        {
            return false;
        }
        NativeBattleContactPacket packet = NativeBattleContactPacket.Decode(proof.ContactPayload.ToArray());
        if (packet.Primary.ActorIndex != proof.RecipientIndex || !HasPhysicalIdentity(before, actor, proof.ActorIndex) || !HasPhysicalIdentity(after, actor, proof.ActorIndex) || !HasPhysicalIdentity(before, proof.RecipientId, proof.RecipientIndex) || !HasPhysicalIdentity(after, proof.RecipientId, proof.RecipientIndex))
        {
            return false;
        }
        return dispatches.EnumerateArray().Count(dispatch => Number(dispatch, "serial") == proof.DispatchSerial && Number(dispatch, "requested_effect") == effect && Number(dispatch, "executed_effect") == effect && Text(dispatch, "actor_id") == actor && Text(dispatch, "ability") == ability && Number(dispatch, "action_id") == nativeAction && OptionalNumber(dispatch, "native_command") == 254 && Number(dispatch, "callback") <= Number(resolution, "callback")) == 1;
    }

    private static bool HasPhysicalIdentity(Dictionary<string, JsonElement> states, string id, byte index)
    {
        return index < 7 && states.TryGetValue(id, out JsonElement state) && OptionalNumber(state, "native_index") == index && states.Values.Count(value => OptionalNumber(value, "native_index") == index) == 1;
    }

    private static long Stock(JsonElement state, long action) => state.GetProperty("magic_stocks").EnumerateArray().Where(stock => Number(stock, "magic_id") == action).Sum(stock => Number(stock, "quantity"));
    private static long Number(JsonElement value, string name) => value.GetProperty(name).GetInt64();
    private static long? OptionalNumber(JsonElement value, string name) => value.TryGetProperty(name, out JsonElement number) && number.ValueKind != JsonValueKind.Null ? number.GetInt64() : null;
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException($"Missing string {name}.");
    private static bool Flag(JsonElement value, string name) => value.GetProperty(name).GetBoolean();

    internal static void RejectDuplicateProperties(JsonElement value)
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
