using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;
using System.Text.Json.Nodes;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TryPrepareSpecialActionRecipient(BattleAssets assets, BattleNativeAction native, JsonObject recipe, out string? blocker)
    {
        blocker = null;
        if (native.AttackType is not (17 or 19))
        {
            return true;
        }
        BattleEncounter? selected = native.AttackType == 19 ? assets.LoadEncounter(368) : SelectCardRecipientFormation(assets);
        if (selected is null)
        {
            blocker = "CARD_TARGET_PRECONDITION: no original single-recipient formation with valid common and rare card records and a targetable initialization was found.";
            return false;
        }
        BattleEncounter formation = selected;
        BattleEnemyPlacement target = formation.Enemies.Single(enemy => enemy.Enabled && !enemy.Hidden && !enemy.InitiallySuspended && !enemy.Untargetable);
        BattleNativeEnemySpecial special = assets.ReadNativeEnemySpecial(target.ModelId);
        string? requirement = ResolveSpecialActionRepeatBlocker(native, special);
        if (requirement is null || requirement.StartsWith("CARD_TARGET_PRECONDITION:", StringComparison.Ordinal) || requirement.StartsWith("DEVOUR_TARGET_PRECONDITION:", StringComparison.Ordinal))
        {
            blocker = requirement ?? "SPECIAL_TARGET_PRECONDITION: original special-action admission is unresolved.";
            return false;
        }
        if (native.AttackType == 19 && special.DevourEffects.Any(effect => effect == byte.MaxValue))
        {
            blocker = requirement;
            return false;
        }
        if (native.AttackType == 19 && (assets.ReadEnemyNativeStatusData(target.ModelId).Saved & 4) != 0)
        {
            blocker = "DEVOUR_TARGET_PRECONDITION: the installed original recipient is petrified and cannot dispatch Devour.";
            return false;
        }
        if (native.AttackType == 19 && Enumerable.Range(1, 100).Any(level => assets.ReadEnemyNativeStats(target.ModelId).Project(level).MaximumHp <= 16))
        {
            blocker = "DEVOUR_REPEAT_PRECONDITION: original target HP must exceed sixteen at every possible allocated level to preserve the recipient through both native eight-damage misses against the one-HP caster.";
            return false;
        }
        string targetId = $"enemy-{target.Slot}";
        recipe["encounter"] = formation.Id;
        recipe["seed"] = 0;
        recipe["target"] = new JsonObject
        {
            ["actor_id"] = targetId,
            ["stage"] = formation.StageId,
            ["model"] = target.ModelId,
            ["scene_slot"] = target.Slot,
            ["physical_index"] = 3,
            ["flags_mask"] = 195,
            ["flags_value"] = 1,
            ["opening_action"] = null,
            ["opening_effect"] = null
        };
        recipe["fixture"]!["combat_party"] = new JsonArray(0, 255, 255);
        recipe["fixture"]!["hold_enemy_commands"] = true;
        if (native.AttackType == 19)
        {
            recipe["fixture"]!["characters"]![0]!["hit_points"] = 1;
        }
        recipe["driver"]!["input"]!["target_id"] = targetId;
        return true;
    }

    private static BattleEncounter? SelectCardRecipientFormation(BattleAssets assets)
    {
        for (int id = 0; id < 1024; id++)
        {
            BattleEncounter formation = assets.LoadEncounter(id);
            BattleEnemyPlacement[] present = formation.Enemies.Where(enemy => enemy.Enabled).ToArray();
            if (present.Length != 1 || present[0] is not { Hidden: false, InitiallySuspended: false, Untargetable: false } target || !target.IsEnemyModel)
            {
                continue;
            }
            BattleNativeEnemySpecial special = assets.ReadNativeEnemySpecial(target.ModelId);
            if (special.CommonCard == byte.MaxValue || special.RareCard == byte.MaxValue || (assets.ReadEnemyNativeStatusData(target.ModelId).Saved & 5) != 0)
            {
                continue;
            }
            if (ScenarioEnemyDiscovery.InitializationUnconditionallyDisablesTargeting(assets.ReadEnemyAi(target.ModelId, 0).Scripts[0]))
            {
                continue;
            }
            return formation;
        }
        return null;
    }

    public static string? ResolveSpecialActionRepeatBlocker(BattleNativeAction native, BattleNativeEnemySpecial target)
    {
        if (native.AttackType == 17)
        {
            if (target.CommonCard == byte.MaxValue && target.RareCard == byte.MaxValue)
            {
                return "CARD_TARGET_PRECONDITION: the installed target DAT has no common or rare card; original admission substitutes a failure message and never dispatches the declared effect. Select an eligible original recipient and prove its two-invocation lifecycle.";
            }
            return "CARD_REPEAT_PRECONDITION: an eligible installed card target does not prove two invocations. Original success ejects the recipient and acquires its card before contact; a proved uninterrupted repeat recipe with its actual chance rolls, inventory capacity and surviving recipient is required.";
        }
        if (native.AttackType == 19)
        {
            if (target.DevourEffects.IsDefault || target.DevourEffects.Length != 3)
            {
                throw new InvalidDataException("Devour discovery requires all three installed target DAT level-band effects.");
            }
            if (target.DevourEffects.All(effect => effect == byte.MaxValue))
            {
                return "DEVOUR_TARGET_PRECONDITION: every installed target DAT level band rejects Devour; original admission substitutes a failure message and never dispatches the declared effect. Select an eligible original recipient and prove its two-invocation lifecycle.";
            }
            return "DEVOUR_REPEAT_PRECONDITION: an eligible installed Devour target does not prove two invocations. Admission requires its actual allocated level-band effect and non-petrified state; success ejects the recipient. A proved uninterrupted repeat recipe with original HP/chance outcomes and a surviving recipient is required.";
        }
        return null;
    }
}
