using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TryPrepareRevivalRecipe(BattleAssets assets, NativeCharacterVitals initial, BattleNativeAction native, string templatePath, out JsonObject? recipe, out string? blocker)
    {
        recipe = null;
        blocker = null;
        BattleAbility ability = native.KernelSection == 1 ? BattleAbility.Magic : BattleAbility.Item;
        BattleActionDefinition? definition = assets.ActionDefinitions.SingleOrDefault(action => action.Ability == ability && action.NativeAction?.Command == native.Command && action.NativeAction.KernelSection == native.KernelSection && action.NativeAction.KernelIndex == native.KernelIndex);
        if (native.AttackType is not (5 or 6) || native.Command is not (2 or 4) || definition is null || !definition.Allies || native.TargetFlags is not byte flags || native.MagicId is 0 or > 400)
        {
            blocker = "REVIVAL_MAPPING_PRECONDITION: require an installed ordinary Magic or Item revival with an allied KO recipient.";
            return false;
        }
        int recipientRow = Enumerable.Range(1, initial.Records.Length - 1).FirstOrDefault(row => initial.Records[row].CharacterId < 8 && initial.Records[row].CharacterId != initial.Records[0].CharacterId, -1);
        if (recipientRow < 0 || !TryPrepareCommand(initial, native.Command, null, out int commandAbility, out int[] guardians, out JsonObject[] guardianRecipes, out blocker))
        {
            blocker ??= "REVIVAL_OWNER_PRECONDITION: require distinct original saved character owners for the living caster and KO recipient.";
            return false;
        }
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        JsonObject fixture = result["fixture"]!.AsObject();
        JsonObject caster = fixture["characters"]![0]!.AsObject();
        caster["row"] = 0;
        caster["character_id"] = initial.Records[0].CharacterId;
        caster["hit_points"] = initial.GetMagicMaximumHitPoints(0);
        caster.Remove("magic_command");
        caster["commands"] = new JsonArray(new JsonObject { ["slot"] = 0, ["ability"] = commandAbility });
        caster["guardian_forces"] = new JsonArray(guardians.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        caster["stocks"] = ability == BattleAbility.Magic ? new JsonArray(new JsonObject { ["action"] = native.ActionId, ["count"] = 2 }) : new JsonArray();
        fixture["guardian_forces"] = new JsonArray(guardianRecipes.Cast<JsonNode>().ToArray());
        fixture["combat_party"] = new JsonArray(0, recipientRow, 255);
        fixture["characters"]!.AsArray().Add(new JsonObject
        {
            ["row"] = recipientRow,
            ["character_id"] = initial.Records[recipientRow].CharacterId,
            ["stocks"] = new JsonArray(),
            ["guardian_forces"] = new JsonArray(),
            ["hit_points"] = 0,
            ["knocked_out"] = true
        });
        if (ability == BattleAbility.Item)
        {
            fixture["items"] = new JsonArray(new JsonObject { ["id"] = definition.Id, ["count"] = 2 });
        }
        // The second selected saved row occupies native physical slot1, independently of its saved-row number.
        _ = NativeBattleLimitRules.PlayerTargetMask(flags, 1);
        result["driver"]!["input"] = new JsonObject { ["kind"] = "action", ["actor_id"] = "party-0", ["target_id"] = $"party-{recipientRow}", ["ability"] = ability.ToString(), ["action_id"] = definition.Id };
        result["driver"]!["expected_effect"] = native.MagicId;
        result["driver"]!["expected_native_action"] = native.ActionId;
        result["driver"]!["expected_native_command"] = native.Command;
        recipe = result;
        return true;
    }
}
