using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class SlotsLiveEvidenceValidation
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    public static void ValidateReport(JsonElement report) => ValidateCore(report, 2);

    public static void ValidateFreshEntryReport(JsonElement report)
    {
        FreshEntryPairValidation.ValidateProtocol(report, true);
        ValidateCore(report, 1);
    }

    private static void ValidateCore(JsonElement report, int expectedExecutions)
    {
        bool declared = report.TryGetProperty("slots_admission", out JsonElement value) && value.ValueKind != JsonValueKind.Null;
        JsonElement original = default;
        JsonElement initialization = default;
        JsonElement driver = default;
        bool originalDeclared = report.TryGetProperty("initialization", out initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.ValueKind == JsonValueKind.Object && preparation.TryGetProperty("driver", out driver) && driver.ValueKind == JsonValueKind.Object && driver.TryGetProperty("slots_admission", out original) && original.ValueKind != JsonValueKind.Null;
        DiagnosticSlotsAdmissionReceipt[] receipts = report.TryGetProperty("slots_admissions", out JsonElement entries) ? entries.Deserialize<DiagnosticSlotsAdmissionReceipt[]>(Json) ?? throw new InvalidDataException("Slots admissions require an explicit array.") : [];
        if (declared != originalDeclared || declared && !JsonElement.DeepEquals(value, original) || !declared && receipts.Length != 0)
        {
            throw new InvalidDataException("Slots admission differs from its initialized declaration.");
        }
        if (!declared)
        {
            if (report.TryGetProperty("expected_native_command", out JsonElement command) && command.ValueKind == JsonValueKind.Number && command.GetInt32() == 16)
            {
                throw new InvalidDataException("Genuine Slots evidence requires its original roll and Do admission receipts.");
            }
            return;
        }
        DiagnosticSlotsDeclaration declaration = value.Deserialize<DiagnosticSlotsDeclaration>(Json) ?? throw new InvalidDataException("Slots declaration is empty.");
        BattleInput input = report.GetProperty("input").Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Slots semantic input is absent.");
        ushort effect = report.GetProperty("expected_effect").GetUInt16();
        declaration.Validate(input, effect, report.GetProperty("expected_native_action").GetInt32(), report.GetProperty("expected_native_command").GetByte());
        if (!JsonElement.DeepEquals(report.GetProperty("input"), driver.GetProperty("input")) || driver.GetProperty("expected_effect").GetUInt16() != effect || driver.GetProperty("expected_native_action").GetInt32() != declaration.MagicAction || driver.GetProperty("expected_native_command").GetByte() != 16 || receipts.Length != expectedExecutions || report.GetProperty("supporting_effects").GetArrayLength() != 0 || report.GetProperty("supporting_invocations").GetArrayLength() != 0 || report.GetProperty("declared_repairs").GetArrayLength() != 0 || report.GetProperty("declared_terminal_hold").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException("Slots evidence requires exactly two genuine independent admissions without substitutions or repairs.");
        }
        foreach (string other in new[] { "shot_cycle", "counter_cycle" })
        {
            if (report.TryGetProperty(other, out JsonElement protocol) && protocol.ValueKind != JsonValueKind.Null)
            {
                throw new InvalidDataException("Slots cannot replace another diagnostic protocol.");
            }
        }
        if (driver.TryGetProperty("enemy_ability", out JsonElement enemy) && enemy.ValueKind != JsonValueKind.Null || initialization.GetProperty("preparation").TryGetProperty("prelude", out JsonElement prelude) && prelude.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException("Slots diagnostics require ordinary original admission without a preparation prelude.");
        }
        DiagnosticSlotsSource source = initialization.GetProperty("slots_source").Deserialize<DiagnosticSlotsSource>(Json) ?? throw new InvalidDataException("Slots original initialization source is absent.");
        source.Validate();
        if (source.BattleInstanceId != Guid.Parse(report.GetProperty("battle_instance_id").GetString()!) || source.BattleInstanceId != Guid.Parse(initialization.GetProperty("battle_instance_id").GetString()!) || source.ActorId != input.ActorId || source.MagicAction != declaration.MagicAction)
        {
            throw new InvalidDataException("Slots source is not owned by this initialized battle and selection.");
        }
        BattleDiagnosticLimitSelection policy = driver.GetProperty("limit_selection").Deserialize<BattleDiagnosticLimitSelection>(Json) ?? throw new InvalidDataException("Slots Limit policy is absent.");
        BattleDiagnosticLimitSelectionReceipt[] selections = report.GetProperty("limit_selections").Deserialize<BattleDiagnosticLimitSelectionReceipt[]>(Json) ?? throw new InvalidDataException("Slots Limit selections are absent.");
        JsonElement[] executions = report.GetProperty("executions").EnumerateArray().ToArray();
        if (receipts.Any(receipt => receipt.Reselections is null) || selections.Length != expectedExecutions + receipts.Sum(receipt => receipt.Reselections.Length) || executions.Length != expectedExecutions)
        {
            throw new InvalidDataException("Slots requires its exact actual initial and reselection log and scored executions.");
        }
        int selectionIndex = 0;
        for (int index = 0; index < expectedExecutions; index++)
        {
            JsonElement execution = executions[index];
            DiagnosticSlotsObservation anchor = execution.GetProperty("slots_start").Deserialize<DiagnosticSlotsObservation>(Json) ?? throw new InvalidDataException("Slots independent scored start is absent.");
            TelemetryOwnerState owners = execution.GetProperty("start_owners").Deserialize<TelemetryOwnerState>(Json) ?? throw new InvalidDataException("Slots execution owner is absent.");
            BattleDiagnosticCombatant[] actors = execution.GetProperty("before").Deserialize<BattleDiagnosticCombatant[]>(Json) ?? throw new InvalidDataException("Slots initial actor observations are absent.");
            BattleDiagnosticCombatant actor = actors.Single(candidate => candidate.Id == source.ActorId);
            DiagnosticSlotsObservation ending = execution.GetProperty("slots_end").Deserialize<DiagnosticSlotsObservation>(Json) ?? throw new InvalidDataException("Slots independent completion observation is absent.");
            DiagnosticSlotsAdmissionReceipt receipt = receipts[index];
            byte learned = (byte)(source.InitialLearnedFlags | 1 << (declaration.MagicAction - 51));
            if (anchor.LearnedFlags != (index == 0 ? source.InitialLearnedFlags : learned) || ending.LearnedFlags != learned || ending.BattleInstanceId != source.BattleInstanceId || ending.LifecycleId != source.LifecycleId || ending.ActorId != source.ActorId || ending.ActorIndex != source.ActorIndex || ending.Callback != execution.GetProperty("end_callback").GetInt64() || ending.RestoreCount != 0 || ending.ResetCount != 0 || ending.OwnedRoll is not null)
            {
                throw new InvalidDataException("Slots learned flag/consumed roll does not follow native dispatch and completion.");
            }
            if (receipt.Execution != index + 1 || receipt.Declaration != declaration || anchor.LifecycleId.ToString() != owners.RetainedMemoryOwnerId || anchor.BattleInstanceId.ToString() != owners.BattleInstanceId || anchor.Callback != owners.NativeCallback || anchor.Callback != execution.GetProperty("start_callback").GetInt64() || anchor.RestoreCount != owners.RestoreCount || anchor.ResetCount != owners.ResetCount || actor.NativeIndex != anchor.ActorIndex || actor.ModelId != anchor.ModelId || actor.CurrentHp != anchor.HitPoints || actor.MaximumHp != anchor.MaximumHitPoints || actor.Statuses0 != anchor.Statuses0 || actor.Statuses1 != anchor.Statuses1 || index > 0 && anchor.Callback <= receipts[index - 1].Do!.After.Callback)
            {
                throw new InvalidDataException("Slots admission anchor differs from independent execution/actor lineage.");
            }
            BattleDiagnosticLimitSelectionReceipt initialSelection = selections[selectionIndex++];
            BattleDiagnosticLimitSelectionReceipt[] reselections = selections.Skip(selectionIndex).Take(receipt.Reselections.Length).ToArray();
            selectionIndex += reselections.Length;
            DiagnosticSlotsAdmissionValidation.ValidateReceipt(receipt, source, anchor, initialSelection, policy, input, reselections);
            DiagnosticSlotsDoReceipt accepted = receipt.Do!;
            if (accepted.After.Callback > execution.GetProperty("end_callback").GetInt64())
            {
                throw new InvalidDataException("Slots Do exceeds its scored execution.");
            }
            BattleDiagnosticEffectDispatch[] dispatches = execution.GetProperty("dispatches").Deserialize<BattleDiagnosticEffectDispatch[]>(Json) ?? throw new InvalidDataException("Slots dispatches are absent.");
            if (dispatches.Length != 1 || dispatches[0] is not { Reflection: null } dispatch || dispatch.ActorId != source.ActorId || dispatch.NativeActorIndex != source.ActorIndex || dispatch.Ability != BattleAbility.LimitBreak || dispatch.NativeCommand != 16 || dispatch.ActionId != declaration.MagicAction || dispatch.RequestedEffect != effect || dispatch.ExecutedEffect != effect || dispatch.Callback < accepted.After.Callback || dispatch.NativeHeaderIndex is null || dispatch.NativeHeaderGeneration is not > 0)
            {
                throw new InvalidDataException("Slots Do is not linked to its actual native command16 dispatch.");
            }
            BattleDiagnosticCombatant target = actors.Single(candidate => candidate.Id == accepted.TargetId);
            if (target.NativeIndex != accepted.TargetIndex)
            {
                throw new InvalidDataException("Slots automatic target was replaced by another physical owner.");
            }
        }
    }
}
