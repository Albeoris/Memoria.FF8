using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static void DeclareEnemySupportingReactions(JsonObject recipe, BattleEncounter formation, string focalActor, ushort focalEffect, IEnumerable<EnemyScenarioScriptedAction> scripted)
    {
        HashSet<BattleDiagnosticSupportingInvocation> declarations = [];
        foreach (BattleEnemyPlacement companion in formation.Slots.Where(slot => slot.Enabled && slot.IsEnemyModel))
        {
            string actor = $"enemy-{companion.Slot}";
            if (actor == focalActor)
            {
                continue;
            }
            foreach (EnemyScenarioScriptedAction action in scripted.Where(action => action.ModelId == companion.ModelId && action.Callback is 2 or 3 && action.Formations.Any(context => context.FormationId == formation.Id && context.SceneSlot == companion.Slot && context.Enabled)))
            {
                if (action.NativeAction is not { MagicId: > 0 } native || action.Command is not byte command || command == 0 || action.ActionId is not ushort actionId || actionId == 0 || native.Command != command || native.ActionId != actionId || action.NativeEffectId != native.MagicId || native.MagicId == focalEffect)
                {
                    continue;
                }
                string prefix = $"battle/c0m{companion.ModelId:000}.dat:ai-callback:{action.Callback}:sha256:";
                string[] hashes = action.MappingEvidence.Where(evidence => evidence.StartsWith(prefix, StringComparison.Ordinal)).Select(evidence => evidence[prefix.Length..].ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
                if (hashes.Length != 1)
                {
                    throw new InvalidDataException("A supporting reaction requires one exact original callback source hash.");
                }
                BattleDiagnosticSupportingInvocation declaration = new(actor, command, actionId, native.MagicId, checked((byte)action.Callback), hashes[0]);
                declaration.Validate();
                declarations.Add(declaration);
                if (declarations.Count > 32)
                {
                    throw new InvalidDataException("The original companion reactions exceed the diagnostic declaration limit of32.");
                }
            }
        }
        if (declarations.Count != 0)
        {
            recipe["driver"]!["supporting_invocations"] = new JsonArray(declarations.OrderBy(item => item.ActorId, StringComparer.Ordinal).ThenBy(item => item.CallbackStage).ThenBy(item => item.NativeCommand).ThenBy(item => item.ActionId).ThenBy(item => item.EffectId).Select(item => (JsonNode)new JsonObject { ["actor_id"] = item.ActorId, ["native_command"] = item.NativeCommand, ["action_id"] = item.ActionId, ["effect_id"] = item.EffectId, ["callback_stage"] = item.CallbackStage, ["script_sha256"] = item.ScriptSha256 }).ToArray());
        }
    }
}
