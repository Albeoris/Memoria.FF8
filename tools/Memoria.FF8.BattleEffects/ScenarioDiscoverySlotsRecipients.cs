using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    private static bool TryPrepareDestructiveSlotsRecipient(BattleAssets assets, BattleNativeAction action, NativeCharacterVitals prepared, JsonObject recipe, out string? blocker)
    {
        blocker = null;
        if ((action.Statuses0 & 1) == 0 && (action.Statuses1 & 0x10000) == 0)
        {
            return true;
        }
        for (int id = 0; id < 1024; id++)
        {
            BattleEncounter formation = assets.LoadEncounter(id);
            BattleEnemyPlacement[] present = formation.Enemies.Where(enemy => enemy.Enabled).ToArray();
            if (present.Length != 1 || present[0] is not { Hidden: false, InitiallySuspended: false, Untargetable: false, IsEnemyModel: true } target)
            {
                continue;
            }
            NativeBattleAiDefinition ai = assets.ReadEnemyAi(target.ModelId, 1);
            NativeBattleStatusAllocation status = assets.ReadEnemyNativeStatusData(target.ModelId);
            if (assets.ReadEnemyNativeCombatantFlags(target.ModelId) != 0x10 || status.Saved != 0 || status.Battle != 0 || ai.Scripts[0].Length == 0 || ai.Scripts[0].Any(value => value != 0) || ai.Scripts[3].Length == 0 || ai.Scripts[3].Any(value => value != 0))
            {
                continue;
            }
            int maximumSpirit = Enumerable.Range(1, 100).Max(level => assets.ReadEnemyNativeStats(target.ModelId).Project(level).Spirit);
            int resistance = (action.Statuses1 & 0x10000) != 0 ? status.Resistances[24] : status.Resistances[0];
            // Native chance255 bypasses resistance;250..254 use the positive effective value without an RNG comparison.
            if (action.StatusAttack != 255 && (action.StatusAttack < 250 || resistance >= 200 || action.StatusAttack - maximumSpirit / 4 - resistance <= 0))
            {
                continue;
            }
            if (!TrySelectStageEntryParty(formation.StageId, formation.Id, prepared.Records, prepared.CombatParty, out byte[]? stageParty, out _) || stageParty is not null && !stageParty.SequenceEqual(prepared.CombatParty))
            {
                continue;
            }
            recipe["encounter"] = formation.Id;
            recipe["target"] = new JsonObject
            {
                ["actor_id"] = $"enemy-{target.Slot}",
                ["stage"] = formation.StageId,
                ["model"] = target.ModelId,
                ["scene_slot"] = target.Slot,
                ["physical_index"] = 3,
                ["flags_mask"] = 195,
                ["flags_value"] = 1,
                ["opening_action"] = null,
                ["opening_effect"] = null
            };
            return true;
        }
        blocker = "SLOTS_DESTRUCTIVE_TARGET_PRECONDITION: require an original single targetable normal enemy with empty initialization/death scripts, no initial statuses, deterministic native KO/ejection susceptibility and an unchanged valid entry party.";
        return false;
    }
}
