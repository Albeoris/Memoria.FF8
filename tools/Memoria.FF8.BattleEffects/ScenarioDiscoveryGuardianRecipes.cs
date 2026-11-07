using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public sealed record IndependentGuardianItemRoute(BattleNativeAction Item, byte? PocketStationAttackVariant);

public static partial class ScenarioDiscovery
{
    public static IndependentGuardianItemRoute? ResolveIndependentGuardianItemRoute(BattleNativeAction child, IReadOnlyList<BattleNativeAction> actions, out string? blocker)
    {
        blocker = null;
        if (child.KernelSection != 9 || child.Command != 244)
        {
            blocker = "INDEPENDENT_GF_TRIGGER_OWNER: command240 retains its learned/reaction owner and command245 retains its automatic support trigger; an ordinary item cannot substitute their dispatch.";
            return null;
        }
        BattleNativeAction[] selected = actions.Where(action => action.KernelSection == 9 && action.Command == 244 && action.ActionId == child.ActionId).ToArray();
        if (selected.Length != 1 || selected[0] != child || child.MagicId is < 1 or > 400)
        {
            blocker = "INDEPENDENT_GF_CHILD_MAPPING: the exact installed command244 child row must have one nonzero effect mapping.";
            return null;
        }
        byte? variant = child.ActionId is >= 2 and <= 5 ? checked((byte)(child.ActionId - 2)) : null;
        BattleNativeAction[] items = actions.Where(action => action.KernelSection == 7 && action.Command == 4 && action.AttackType == 14 && (variant is not null ? action.ActionId == 30 : action.ActionId != 30 && action.MagicId == child.ActionId)).ToArray();
        if (items.Length != 1 || items[0].TargetFlags is null)
        {
            blocker = "INDEPENDENT_GF_ITEM_MAPPING: no unique ordinary installed item with its legal target flags produces this exact child; retain the original learned or automatic trigger owner.";
            return null;
        }
        return new(items[0], variant);
    }

    public static bool TryPrepareIndependentGuardianRecipe(BattleAssets assets, NativeCharacterVitals initial, BattleNativeAction child, string templatePath, out JsonObject? recipe, out string? blocker)
    {
        recipe = null;
        IndependentGuardianItemRoute? route = ResolveIndependentGuardianItemRoute(child, assets.NativeActions, out blocker);
        if (route is null)
        {
            return false;
        }
        if (route.PocketStationAttackVariant is null)
        {
            return TryPrepareOrdinaryPlayerRecipe(assets, initial, route.Item, templatePath, out recipe, out blocker);
        }
        BattleActionDefinition? definition = assets.ActionDefinitions.SingleOrDefault(action => action.Ability == BattleAbility.Item && action.NativeAction is { Command: 4, KernelSection: 7 } native && native.ActionId == route.Item.ActionId && native.KernelIndex == route.Item.KernelIndex);
        if (definition is null || !TryPrepareCommand(initial, 4, null, out int ability, out int[] guardians, out JsonObject[] guardianRecipes, out blocker))
        {
            blocker ??= "INDEPENDENT_GF_ITEM_DEFINITION: the installed Gysahl Greens ordinary item definition is unavailable.";
            return false;
        }
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        JsonObject fixture = result["fixture"]!.AsObject();
        JsonObject character = fixture["characters"]![0]!.AsObject();
        fixture["guardian_forces"] = new JsonArray(guardianRecipes.Cast<JsonNode>().ToArray());
        character["guardian_forces"] = new JsonArray(guardians.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        character["stocks"] = new JsonArray();
        character.Remove("magic_command");
        character["commands"] = new JsonArray(new JsonObject { ["slot"] = 0, ["ability"] = ability });
        character["hit_points"] = initial.GetMagicMaximumHitPoints(0);
        fixture["items"] = new JsonArray(new JsonObject { ["id"] = definition.Id, ["count"] = 2 });
        fixture["pocket_station"] = new JsonObject { ["available"] = true, ["away"] = false, ["level"] = 1, ["attack_variant"] = route.PocketStationAttackVariant.Value };
        bool party = definition.Allies || NativeBattleLimitRules.IsFixedAllyTarget(route.Item.TargetFlags!.Value);
        (string recipient, int recipientIndex) = ResolveRecipeRecipient(result, party, "party-0");
        _ = NativeBattleLimitRules.PlayerTargetMask(route.Item.TargetFlags!.Value, recipientIndex);
        result["driver"]!["input"] = new JsonObject { ["kind"] = "action", ["actor_id"] = "party-0", ["target_id"] = recipient, ["ability"] = nameof(BattleAbility.Item), ["action_id"] = definition.Id };
        result["driver"]!["expected_effect"] = child.MagicId;
        result["driver"]!["expected_native_action"] = child.ActionId;
        result["driver"]!["expected_native_command"] = child.Command;
        recipe = result;
        return true;
    }
}
