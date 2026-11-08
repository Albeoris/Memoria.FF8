using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    private static bool TryPrepareDuelRecipe(BattleAssets assets, NativeCharacterVitals initial, NativeBattleExperienceDefinition experience, BattleNativeAction native, string templatePath, out JsonObject? recipe, out string? blocker)
    {
        recipe = null;
        blocker = null;
        const int owner = 1;
        int row = Array.FindIndex(initial.Records, record => record.CharacterId == owner);
        BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(owner);
        if (row < 0 || initial.Records.Count(record => record.CharacterId == owner) != 1 || native.CharacterId != owner || rules.CharacterId != owner || rules.Command != 11 || NativeBattleDuelRules.Timer(rules, 1) <= 0)
        {
            blocker = "DUEL_OWNER_PRECONDITION: original Zell, learned native basic and crisis1 timer are required.";
            return false;
        }
        NativeCharacterVitals prepared = (initial with { CombatParty = [checked((byte)row), 255, 255] }).WithDiagnosticBattleLevel(row, 100, experience);
        int hp = ResolveLimitPreparationHitPoints(rules, prepared.GetMagicMaximumHitPoints(row), prepared.Records[row].Status, 1);
        if (hp == 0)
        {
            blocker = "DUEL_CRISIS_PRECONDITION: the original living Zell preparation cannot select crisis1.";
            return false;
        }
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        JsonObject fixture = result["fixture"]!.AsObject();
        JsonObject character = fixture["characters"]![0]!.AsObject();
        character["row"] = row;
        character["character_id"] = owner;
        character["level"] = 100;
        character["hit_points"] = hp;
        character["stocks"] = new JsonArray();
        character["guardian_forces"] = new JsonArray();
        character.Remove("magic_command");
        character.Remove("commands");
        fixture["combat_party"] = new JsonArray(row, 255, 255);
        fixture["guardian_forces"] = new JsonArray();
        fixture["hold_enemy_commands"] = true;
        fixture["limits"] = new JsonObject { ["learn_duel"] = 0x3F, ["options978"] = 0 };
        string actor = $"party-{row}";
        (string target, int targetIndex) = ResolveRecipeRecipient(result, false, actor);
        _ = NativeBattleLimitRules.PlayerTargetMask(rules.InvocationChoices.Single().TargetFlags, targetIndex);
        JsonObject driver = result["driver"]!.AsObject();
        driver["input"] = new JsonObject { ["kind"] = "action", ["actor_id"] = actor, ["target_id"] = target, ["ability"] = nameof(BattleAbility.LimitBreak), ["action_id"] = 11 << 16 };
        driver["expected_effect"] = 274;
        driver["expected_native_command"] = 241;
        driver["expected_native_action"] = native.ActionId;
        driver["limit_selection"] = JsonSerializer.SerializeToNode(new BattleDiagnosticLimitSelection(1, 1, 256), RecordJson.Options);
        driver["duel_cycle"] = JsonSerializer.SerializeToNode(new DiagnosticDuelCycleDeclaration(checked((byte)native.ActionId)), RecordJson.Options);
        driver["repairs"] = new JsonArray();
        driver["supporting_effects"] = new JsonArray();
        driver["supporting_invocations"] = new JsonArray();
        foreach (string incompatible in new[] { "fresh_entry", "terminal_hold", "enemy_ability", "shot_cycle", "slots_admission", "counter_cycle", "presentation" })
        {
            driver.Remove(incompatible);
        }
        recipe = result;
        return true;
    }
}
