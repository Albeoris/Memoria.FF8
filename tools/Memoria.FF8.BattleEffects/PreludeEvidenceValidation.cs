using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.BattleEffects;

public static class PreludeEvidenceValidation
{
    public static JsonElement ResolveSubmittedInput(JsonElement bootstrap, JsonElement report)
    {
        JsonElement originalInput = bootstrap.GetProperty("driver").GetProperty("input");
        bool declared = bootstrap.TryGetProperty("prelude", out JsonElement declaration) && declaration.ValueKind != JsonValueKind.Null;
        JsonElement receipt = default;
        bool recorded = report.TryGetProperty("initialization", out JsonElement initialization) && initialization.TryGetProperty("prelude", out receipt) && receipt.ValueKind != JsonValueKind.Null;
        if (!declared)
        {
            Require(!recorded, "An undeclared prelude cannot rebind the submitted actor.");
            return originalInput;
        }
        Require(recorded, "Declared ordinary-command preparation requires its retained native phase receipt.");
        JsonElement observedDeclaration = receipt.GetProperty("declaration");
        Require(PilotRunner.SameSemanticInput(declaration.GetProperty("input"), observedDeclaration.GetProperty("input")), "Prelude ordinary command differs from its frozen declaration.");
        foreach (string name in new[] { "defeated_model", "defeated_scene_slot", "death_script_sha256", "require_native_allocation" })
        {
            Require(JsonElement.DeepEquals(declaration.GetProperty(name), observedDeclaration.GetProperty(name)), "Prelude source identity differs from its frozen declaration.");
        }
        foreach ((string name, int fallback) in new[] { ("maximum_attacks", 256), ("maximum_callbacks", 18000) })
        {
            Require((declaration.TryGetProperty(name, out JsonElement bound) ? bound.GetInt32() : fallback) == observedDeclaration.GetProperty(name).GetInt32(), "Prelude bounds differ from the submitted declaration.");
        }
        JsonElement target = bootstrap.GetProperty("target");
        JsonElement beforeTarget = receipt.GetProperty("original_target");
        JsonElement afterTarget = receipt.GetProperty("resolved_target");
        JsonElement liveTarget = receipt.GetProperty("target");
        Require(JsonElement.DeepEquals(target, beforeTarget), "Prelude changed the declared target contract.");
        foreach (string name in new[] { "stage", "model", "scene_slot", "physical_index", "flags_mask", "flags_value", "opening_action", "opening_effect" })
        {
            Require(JsonElement.DeepEquals(beforeTarget.GetProperty(name), afterTarget.GetProperty(name)), "Native phase binding cannot relax target validation.");
        }
        string originalActor = Text(beforeTarget, "actor_id");
        string resolvedActor = Text(afterTarget, "actor_id");
        Require(Text(originalInput, "actor_id") == originalActor, "Prelude can bind only its declared focal enemy owner.");
        Require(Text(liveTarget, "actor_id") == resolvedActor && Number(liveTarget, "model") == Number(target, "model") && Number(liveTarget, "scene_slot") == Number(target, "scene_slot") && Number(liveTarget, "physical_index") == Number(target, "physical_index") && Number(liveTarget, "stage") == Number(target, "stage") && liveTarget.GetProperty("alive").GetBoolean() && liveTarget.GetProperty("statistics_ready").GetBoolean() && (Number(liveTarget, "flags") & Number(target, "flags_mask")) == Number(target, "flags_value"), "Prelude did not retain an initialized native target satisfying the original contract.");
        Require(Text(receipt, "battle_instance_id") == Text(report, "battle_instance_id") && Number(receipt, "restore_count") == 0 && Number(receipt, "reset_count") == 0 && Number(receipt, "start_callback") == 0 && Number(receipt, "end_callback") > 0 && Number(receipt, "attacks") is >= 1 and <= 1024 && Number(receipt, "attacks") <= Number(observedDeclaration, "maximum_attacks") && Number(receipt, "end_callback") <= Number(observedDeclaration, "maximum_callbacks") && Number(receipt, "predecessor_hit_points_before") > 0 && Number(receipt, "predecessor_hit_points_after") == 0, "Prelude continuity, ordinary attacks or genuine predecessor KO is unproved.");
        double elapsed = receipt.GetProperty("milliseconds").GetDouble();
        Require(double.IsFinite(elapsed) && elapsed >= 0, "Prelude wall time must be retained.");
        long expectedEffect = Number(report, "expected_effect");
        Require(receipt.GetProperty("dispatches").EnumerateArray().All(dispatch => Number(dispatch, "requested_effect") != expectedEffect && Number(dispatch, "executed_effect") != expectedEffect), "Prelude warmed the focal effect before its measured first invocation.");
        JsonElement attack = declaration.GetProperty("input");
        Require(string.Equals(Text(attack, "kind"), "Action", StringComparison.OrdinalIgnoreCase) && string.Equals(Text(attack, "ability"), "Attack", StringComparison.OrdinalIgnoreCase) && Number(attack, "action_id") == 0, "Prelude must use the declared ordinary Attack producer.");
        JsonElement[] events = receipt.GetProperty("events").EnumerateArray().ToArray();
        Require(events.Any(value => Text(value, "kind") == "Damage" && Text(value, "actor_id") == Text(attack, "actor_id") && Text(value, "target_id") == Text(attack, "target_id") && Number(value, "amount") > 0), "Prelude has no ordinary attack contact on its actual predecessor.");
        bool spawned = events.Any(value => Text(value, "kind") == "NativeSpawn" && Text(value, "actor_id") == Text(attack, "target_id") && Text(value, "target_id") == resolvedActor && Number(value, "amount") == Number(target, "scene_slot"));
        bool requireAllocation = declaration.GetProperty("require_native_allocation").GetBoolean();
        Require(spawned == requireAllocation && (requireAllocation ? resolvedActor != originalActor : resolvedActor == originalActor), "Prelude actor binding lacks the declared original allocation/activation provenance.");
        Require(Number(receipt, "predecessor_physical_index") is >= 3 and <= 6, "Prelude predecessor physical owner is outside the native enemy bank.");
        Require(PilotRunner.SameSemanticInput(originalInput, receipt.GetProperty("original_input")), "Prelude original focal input differs from the submitted recipe.");
        JsonObject resolved = JsonNode.Parse(originalInput.GetRawText())!.AsObject();
        resolved["actor_id"] = resolvedActor;
        if (Text(originalInput, "target_id") == originalActor)
        {
            resolved["target_id"] = resolvedActor;
        }
        using JsonDocument normalized = JsonDocument.Parse(resolved.ToJsonString());
        Require(PilotRunner.SameSemanticInput(normalized.RootElement, receipt.GetProperty("resolved_input")), "Prelude changed focal semantics beyond its proved native owner binding.");
        foreach (JsonElement execution in report.GetProperty("executions").EnumerateArray())
        {
            Require(Number(receipt, "end_callback") <= Number(execution, "start_callback") && Text(receipt, "lifecycle_id") == Text(execution.GetProperty("start_owners"), "retained_memory_owner_id"), "Prelude and focal executions do not share one uninterrupted native lifecycle.");
        }
        JsonElement selection = bootstrap.GetProperty("driver").GetProperty("enemy_ability");
        foreach (JsonElement submission in report.GetProperty("enemy_submissions").EnumerateArray())
        {
            JsonElement actualSelection = submission.GetProperty("selection");
            Require(Text(submission, "actor_id") == resolvedActor && Text(submission, "lifecycle_id") == Text(receipt, "lifecycle_id") && Number(actualSelection, "model") == Number(selection, "model") && Number(actualSelection, "scene_slot") == Number(selection, "scene_slot") && Number(actualSelection, "level_band") == Number(selection, "level_band"), "The observed phase allocation did not preserve its declared model, scene and original level band.");
        }
        return normalized.RootElement.Clone();
    }

    public static void ValidateReport(JsonElement report)
    {
        if (!report.TryGetProperty("initialization", out JsonElement initialization))
        {
            return;
        }
        bool declared = initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.TryGetProperty("prelude", out JsonElement declaration) && declaration.ValueKind != JsonValueKind.Null;
        bool recorded = initialization.TryGetProperty("prelude", out JsonElement receipt) && receipt.ValueKind != JsonValueKind.Null;
        if (!declared && !recorded)
        {
            return;
        }
        Require(declared && recorded, "Prelude declaration and native continuation receipt must both be retained.");
        JsonElement resolved = ResolveSubmittedInput(preparation, report);
        Require(PilotRunner.SameSemanticInput(resolved, report.GetProperty("input")), "Live focal input differs from its proved native phase binding.");
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? string.Empty;
    private static long Number(JsonElement value, string name) => value.GetProperty(name).GetInt64();
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
