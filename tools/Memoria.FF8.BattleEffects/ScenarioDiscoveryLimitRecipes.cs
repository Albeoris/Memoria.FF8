using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool IsTypedLimitScenario(BattleNativeAction native) => native is { KernelSection: 21, Command: 237, ActionId: <= 7 } or { KernelSection: 1, Command: 16, ActionId: 51 or 52 or 53 or 56 };

    public static bool TryPrepareTypedLimitRecipe(BattleAssets assets, NativeCharacterVitals initial, NativeBattleExperienceDefinition experience, BattleNativeAction native, string templatePath, string gameDirectory, out JsonObject? recipe, out string? blocker, IDictionary<string, DiagnosticFreshEntryDeclaration>? sourcePins = null)
    {
        recipe = null;
        blocker = null;
        if (!IsTypedLimitScenario(native) || native.MagicId is 0 or > 400 || native.TargetFlags is null)
        {
            blocker = "LIMIT_PROTOCOL_PRECONDITION: this action has no supported ordinary Slots or Shot protocol.";
            return false;
        }
        bool slots = native.Command == 16;
        int owner = slots ? 5 : 2;
        byte invocation = slots ? (byte)16 : (byte)14;
        if (!slots && native.CharacterId != owner)
        {
            blocker = "LIMIT_OWNER_PRECONDITION: the installed Shot row does not identify its original Irvine owner.";
            return false;
        }
        int row = Array.FindIndex(initial.Records, record => record.CharacterId == owner);
        BattleNativeLimitDefinition rules = assets.ReadNativeLimitDefinition(owner);
        if (row < 0 || initial.Records.Count(record => record.CharacterId == owner) != 1 || rules.CharacterId != owner || rules.Command != invocation)
        {
            blocker = "LIMIT_OWNER_PRECONDITION: the original character record and installed Limit command must agree.";
            return false;
        }
        int initialLevel = NativeStatDefinitions.Level(initial.Records[row].Experience, initial.Definitions.Characters[owner]);
        int level = slots ? ResolveSlotsPreparationLevel(rules, checked((byte)native.ActionId), initialLevel) : 100;
        if (level == 0)
        {
            blocker = "SLOTS_ROLL_PRECONDITION: the original tables do not offer this spell at a supported prepared level and crisis4.";
            return false;
        }
        NativeCharacterVitals prepared = (initial with { CombatParty = [checked((byte)row), 255, 255] }).WithDiagnosticBattleLevel(row, level, experience);
        ushort maximum = prepared.GetMagicMaximumHitPoints(row);
        int hp = ResolveLimitPreparationHitPoints(rules, maximum, prepared.Records[row].Status, 4);
        if (hp == 0)
        {
            blocker = "LIMIT_CRISIS_PRECONDITION: no positive HP can produce the required native crisis selection.";
            return false;
        }
        JsonObject result = JsonNode.Parse(File.ReadAllText(templatePath))!.AsObject();
        JsonObject fixture = result["fixture"]!.AsObject();
        JsonObject character = fixture["characters"]![0]!.AsObject();
        character["row"] = row;
        character["character_id"] = owner;
        character["level"] = level;
        character["hit_points"] = hp;
        character["stocks"] = new JsonArray();
        character["guardian_forces"] = new JsonArray();
        character.Remove("magic_command");
        character.Remove("commands");
        fixture["guardian_forces"] = new JsonArray();
        fixture["combat_party"] = new JsonArray(row, 255, 255);
        fixture["hold_enemy_commands"] = true;
        string actor = $"party-{row}";
        bool party = slots && ((native.TargetFlags.Value & 0x40) == 0 || NativeBattleLimitRules.IsFixedAllyTarget(native.TargetFlags.Value));
        if (slots && !party && !TryPrepareDestructiveSlotsRecipient(assets, native, prepared, result, out blocker))
        {
            return false;
        }
        (string target, int targetIndex) = ResolveRecipeRecipient(result, party, actor);
        _ = NativeBattleLimitRules.PlayerTargetMask(native.TargetFlags.Value, targetIndex);
        JsonObject driver = result["driver"]!.AsObject();
        driver["input"] = new JsonObject { ["kind"] = "action", ["actor_id"] = actor, ["target_id"] = target, ["ability"] = nameof(BattleAbility.LimitBreak), ["action_id"] = invocation << 16 | native.ActionId };
        driver["expected_effect"] = native.MagicId;
        driver["expected_native_action"] = native.ActionId;
        driver["expected_native_command"] = native.Command;
        driver["limit_selection"] = JsonSerializer.SerializeToNode(new BattleDiagnosticLimitSelection(slots ? (byte)4 : (byte)1, 4, 256), RecordJson.Options);
        driver["repairs"] = new JsonArray();
        driver["supporting_effects"] = new JsonArray();
        driver["supporting_invocations"] = new JsonArray();
        driver.Remove("terminal_hold");
        driver.Remove("enemy_ability");
        if (slots)
        {
            DiagnosticSlotsDeclaration declaration = new(checked((byte)native.ActionId), 256, 5) { Reselection = DiagnosticSlotsDeclaration.ReadySelfBatch2 };
            if (declaration.Effect != native.MagicId)
            {
                blocker = "SLOTS_MAPPING_PRECONDITION: the installed special spell and supported admission identity disagree.";
                return false;
            }
            fixture["limits"] = new JsonObject { ["learn_special_magic"] = 1 << (native.ActionId - 51) };
            driver["slots_admission"] = JsonSerializer.SerializeToNode(declaration, RecordJson.Options);
            if (!TryConfigureFreshEntryExecutionProfile(result, gameDirectory, out blocker, sourcePins))
            {
                return false;
            }
        }
        else
        {
            BattleNativeAction? opening = assets.NativeActions.SingleOrDefault(action => action.KernelSection == 21 && action.Command == 14 && action.ActionId == native.ActionId);
            BattleNativeAction? ending = assets.NativeActions.SingleOrDefault(action => action.KernelSection == 21 && action.Command == 238 && action.ActionId == native.ActionId);
            if (opening is null || ending is null || opening.MagicId == 0 || ending.MagicId == 0 || rules.ShotTimers.Length != 4 || rules.ShotTimers.Any(timer => timer == 0))
            {
                blocker = "SHOT_CHAIN_PRECONDITION: original opening/firing/ending rows and all native timers are required.";
                return false;
            }
            fixture["items"] = new JsonArray(new JsonObject { ["id"] = 101 + native.ActionId, ["count"] = 3 });
            driver["shot_cycle"] = JsonSerializer.SerializeToNode(new DiagnosticShotCycleDeclaration(checked((byte)native.ActionId), opening.MagicId, ending.MagicId), RecordJson.Options);
        }
        recipe = result;
        return true;
    }

    public static int ResolveSlotsPreparationLevel(BattleNativeLimitDefinition rules, byte magic, int minimumLevel = 1)
    {
        int chosen = 0;
        int best = 0;
        int[] buckets = [0, 39, 159, 209, 249];
        int[] weights = [39, 120, 50, 40, 7];
        for (int level = minimumLevel; level <= 100; level++)
        {
            int available = 0;
            for (int bucket = 0; bucket < buckets.Length; bucket++)
            {
                for (byte levelRoll = 0; levelRoll < 5; levelRoll++)
                {
                    for (byte spell = 0; spell < 8; spell++)
                    {
                        BattleNativeSlotsRoll roll = NativeBattleSlotsRules.Roll(rules, level, 4, levelRoll, checked((byte)buckets[bucket]), spell, 0);
                        if (roll.MagicId == magic)
                        {
                            available += weights[bucket];
                        }
                    }
                }
            }
            if (available > best)
            {
                chosen = level;
                best = available;
            }
        }
        return chosen;
    }
}
