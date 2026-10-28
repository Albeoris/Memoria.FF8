using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static JsonObject? CreateProvedEnemyPrelude(BattleAssets assets, EnemyScenarioActionOwner owner, EnemyScenarioFormation context)
    {
        bool activation = owner.ModelId == 100 && context.FormationId == 120 && context.SceneSlot == 2;
        bool allocation = owner.ModelId == 122 && context.FormationId == 511 && context.SceneSlot == 2;
        if (!activation && !allocation)
        {
            return null;
        }
        BattleEncounter formation = assets.LoadEncounter(context.FormationId);
        BattleEnemyPlacement source = formation.Slots.Single(slot => slot.Slot == 0);
        BattleEnemyPlacement target = formation.Slots.Single(slot => slot.Slot == context.SceneSlot);
        int expectedSource = activation ? 87 : 121;
        if (!source.Enabled || source.ModelId != expectedSource || source.Hidden || source.InitiallySuspended || source.Untargetable || target.ModelId != owner.ModelId || target.Enabled == allocation)
        {
            throw new InvalidDataException("The selected ordinary-command phase carrier changed its original predecessor or reserved target.");
        }
        byte[] death = assets.ReadEnemyAi(source.ModelId, 0).Scripts[3];
        // These are installed script witnesses, not participant mutations or replacement AI.
        if (!HasPreludeDeathTransition(death, allocation) || allocation && !formation.Slots.Any(slot => slot.ModelId == 123 && slot.Enabled && slot.InitiallySuspended))
        {
            throw new InvalidDataException("The original predecessor death callback no longer proves this activation/allocation route.");
        }
        JsonObject declaration = new()
        {
            ["input"] = new JsonObject { ["kind"] = "Action", ["actor_id"] = "party-0", ["target_id"] = "enemy-0", ["ability"] = "Attack", ["action_id"] = 0 },
            ["defeated_model"] = source.ModelId,
            ["defeated_scene_slot"] = source.Slot,
            ["death_script_sha256"] = Convert.ToHexStringLower(SHA256.HashData(death)),
            ["require_native_allocation"] = allocation,
            ["maximum_attacks"] = 256,
            ["maximum_callbacks"] = 18000
        };
        SchemaValidation.ValidateBytes(System.Text.Encoding.UTF8.GetBytes(declaration.ToJsonString()), "ordinary-attack-prelude");
        return declaration;
    }

    public static bool HasPreludeDeathTransition(ReadOnlySpan<byte> script, bool allocation)
    {
        ReadOnlySpan<byte> witness = allocation ? [29, 200, 51, 31, 2] : [58, 5, 44];
        for (int pc = 0; pc < script.Length;)
        {
            byte opcode = script[pc];
            int width = ScenarioEnemyDiscovery.InstructionWidth(opcode);
            if (opcode == 0 || opcode > 61 || width > script.Length - pc)
            {
                return false;
            }
            if (witness.Length <= script.Length - pc && script.Slice(pc, witness.Length).SequenceEqual(witness))
            {
                return true;
            }
            pc += width;
        }
        return false;
    }
}
