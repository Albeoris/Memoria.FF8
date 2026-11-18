using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class LiveEvidenceValidation
{
    private static bool HasNativeStatusRemovalProvenance(JsonElement execution, JsonElement resolution, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after)
    {
        BattleDiagnosticNativeStatusRemoval? proof = resolution.GetProperty("native_status_removal").Deserialize<BattleDiagnosticNativeStatusRemoval>(Json);
        string actor = Text(resolution, "actor_id");
        string recipient = Text(resolution, "actual_target_id");
        if (proof is null || proof.ProducerAddress != 0x494410 || (proof.ActionStatuses1 & 0x10000) == 0 || (proof.AppliedBattleStatuses & 0x10000) == 0 || Text(resolution, "kind") != "damage" || recipient != Text(resolution, "original_target_id") || !HasObservedBattleIdentity(before, actor, proof.ActorSlot, proof.ActorIndex) || !HasObservedBattleIdentity(after, actor, proof.ActorSlot, proof.ActorIndex) || !HasObservedBattleIdentity(before, recipient, proof.RecipientSlot, proof.RecipientIndex) || !HasStatusRemovalRecipientAfter(execution, resolution, before, after, recipient, proof) || Text(resolution, "hp_owner") != HpOwner(proof.RecipientSlot, proof.RecipientIndex) || Number(resolution, "resolved_amount") < 0 || Number(resolution, "actual_amount") != Number(resolution, "before_hp") || Number(resolution, "before_hp") <= 0 || Number(resolution, "after_hp") != 0 || Number(after[recipient], "current_hp") != 0 || (Number(after[recipient], "statuses0") & 1) == 0)
        {
            return false;
        }
        return execution.GetProperty("dispatches").EnumerateArray().Any(dispatch => Text(dispatch, "actor_id") == actor && OptionalNumber(dispatch, "native_actor_index") == proof.ActorIndex && OptionalNumber(dispatch, "native_command") == proof.NativeCommand && Number(dispatch, "action_id") == proof.NativeActionId && IncludesReachedRecipient(dispatch, recipient, proof.RecipientIndex) && Number(dispatch, "callback") >= Number(resolution, "callback"));
    }

    private static bool HasStatusRemovalRecipientAfter(JsonElement execution, JsonElement resolution, Dictionary<string, JsonElement> before, Dictionary<string, JsonElement> after, string recipient, BattleDiagnosticNativeStatusRemoval proof)
    {
        if (HasObservedBattleIdentity(after, recipient, proof.RecipientSlot, proof.RecipientIndex))
        {
            return true;
        }
        if (!before.TryGetValue(recipient, out JsonElement previous) || !after.TryGetValue(recipient, out JsonElement retired) || ReadObservedRosterSlot(previous) is not { Side: BattleSide.Allies } slot ||
            !retired.TryGetProperty("native_index", out JsonElement alias) || alias.ValueKind != JsonValueKind.Null || !retired.TryGetProperty("slot", out JsonElement placement) || placement.ValueKind != JsonValueKind.Null ||
            OptionalNumber(previous, "model_id") is not long model || OptionalNumber(retired, "model_id") != model || OptionalNumber(previous, "maximum_hp") is not > 0 || OptionalNumber(retired, "maximum_hp") != OptionalNumber(previous, "maximum_hp") ||
            after.Values.Any(candidate => ReadObservedRosterSlot(candidate) == slot || proof.RecipientIndex is byte original && OptionalNumber(candidate, "native_index") == original) ||
            !execution.TryGetProperty("events", out JsonElement events) || events.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        long resolutionFrame = Number(resolution, "frame");
        long endFrame = Number(execution, "end_frame");
        return events.EnumerateArray().Count(entry => entry.TryGetProperty("kind", out JsonElement kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "Ejected" &&
            entry.TryGetProperty("target_id", out JsonElement target) && target.ValueKind == JsonValueKind.String && target.GetString() == recipient &&
            entry.TryGetProperty("actor_id", out JsonElement source) && source.ValueKind == JsonValueKind.Null && OptionalNumber(entry, "amount") == 0 &&
            OptionalNumber(entry, "frame") is long frame && frame >= resolutionFrame && frame <= endFrame) == 1;
    }
}
