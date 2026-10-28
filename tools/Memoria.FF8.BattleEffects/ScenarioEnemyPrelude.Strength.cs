using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static void PrepareEnemyPreludeStrength(JsonObject recipe, NativeCharacterVitals source, BattleNativePlayerStatRules rules)
    {
        source.Validate();
        rules.Validate();
        JsonObject fixture = recipe["fixture"]!.DeepClone().AsObject();
        int row = fixture["combat_party"]?[0]?.Deserialize<int>() ?? source.CombatParty[0];
        if ((uint)row >= 8)
        {
            throw new InvalidDataException("Ordinary attack preparation requires a present saved party row.");
        }
        JsonObject character = fixture["characters"]!.AsArray().Select(value => value!.AsObject()).Single(value => value["row"]!.Deserialize<int>() == row);
        if (source.Records[row].CharacterId != character["character_id"]!.Deserialize<int>() || rules.CharacterId != source.Records[row].CharacterId)
        {
            throw new InvalidDataException("Ordinary attack preparation requires the actual first party member and its installed statistic rules.");
        }
        NativeCharacterVitals prepared = source.DeepCopy();
        JsonArray guardians = fixture["guardian_forces"]!.AsArray();
        JsonArray assignments = character["guardian_forces"]!.AsArray();
        foreach (JsonObject declaration in guardians.Select(value => value!.AsObject()))
        {
            int id = declaration["id"]!.Deserialize<int>();
            bool acquire = declaration["acquire"]?.GetValue<bool>() ?? throw new InvalidDataException("Ordinary attack preparation requires an explicit GF acquisition declaration.");
            if (acquire)
            {
                prepared = prepared.AcquireGuardianForce(id);
            }
            if (declaration["learning_ability"] is { } learning)
            {
                prepared = prepared.SelectGuardianForceLearning(id, learning.Deserialize<int>());
            }
            if (declaration["ability_points"] is { } points)
            {
                prepared = prepared.AwardGuardianForceAbilityPoints(id, checked((ushort)points.Deserialize<int>()));
            }
        }
        foreach (JsonNode? assignment in assignments)
        {
            int id = assignment!.Deserialize<int>();
            if ((prepared.Records[row].Junction.GuardianForces & (1 << id)) == 0)
            {
                prepared = prepared.ToggleGuardianForceJunction(row, id);
            }
        }
        if (!prepared.JunctionMagicSlotAvailable(row, 1))
        {
            bool found = false;
            foreach (int id in Enumerable.Range(0, prepared.GuardianForces.Length).OrderBy(id => (prepared.Records[row].Junction.GuardianForces & (1 << id)) == 0 ? 1 : 0).ThenBy(id => id))
            {
                if (prepared.Records.Where((_, index) => index != row).Any(record => (record.Junction.GuardianForces & (1 << id)) != 0) || fixture["characters"]!.AsArray().Where(value => value!["row"]!.Deserialize<int>() != row).Any(value => value!["guardian_forces"]!.AsArray().Any(gf => gf!.Deserialize<int>() == id)))
                {
                    continue;
                }
                NativeCharacterVitals candidate = prepared.AcquireGuardianForce(id);
                JsonObject? existing = guardians.Select(value => value!.AsObject()).SingleOrDefault(value => value["id"]!.Deserialize<int>() == id);
                int award = 0;
                if (!candidate.GuardianForces[id].HasLearnedAbility(2))
                {
                    NativeGuardianForceAbility? ability = candidate.GuardianForceAbilities(id).SingleOrDefault(value => value.Ability == 2 && !value.Learned);
                    if (ability is null || existing is not null)
                    {
                        continue;
                    }
                    award = Math.Max(1, ability.RequiredPoints - ability.Progress);
                    candidate = candidate.SelectGuardianForceLearning(id, 2).AwardGuardianForceAbilityPoints(id, checked((ushort)award));
                }
                if (!candidate.GuardianForces[id].HasLearnedAbility(2))
                {
                    continue;
                }
                if ((candidate.Records[row].Junction.GuardianForces & (1 << id)) == 0)
                {
                    candidate = candidate.ToggleGuardianForceJunction(row, id);
                }
                if (!candidate.JunctionMagicSlotAvailable(row, 1))
                {
                    continue;
                }
                if (existing is null)
                {
                    JsonObject declaration = new() { ["id"] = id, ["acquire"] = true };
                    if (award != 0)
                    {
                        declaration["learning_ability"] = 2;
                        declaration["ability_points"] = award;
                    }
                    guardians.Add(declaration);
                }
                if (!assignments.Any(value => value!.Deserialize<int>() == id))
                {
                    assignments.Add(id);
                }
                prepared = candidate;
                found = true;
                break;
            }
            if (!found)
            {
                throw new NotSupportedException("Ordinary attack preparation has no available Strength junction provider without replacing another GF learning prerequisite.");
            }
        }
        JsonArray stocks = character["stocks"]!.AsArray();
        foreach (JsonNode? stock in stocks)
        {
            int spell = stock!["action"]!.Deserialize<int>();
            int count = stock["count"]!.Deserialize<int>();
            int delta = count - prepared.GetMagicCount(row, spell);
            prepared = prepared.ChangeMagicStock(row, spell, delta, delta >= 0 ? MagicOperationReason.ScriptReward : MagicOperationReason.ScriptRemoval, out MagicStockMutation mutation);
            if (mutation.Rejected != 0)
            {
                throw new InvalidOperationException("Ordinary attack preparation could not preserve its declared magic stock.");
            }
        }
        JsonArray junctions = character["magic_junctions"]?.AsArray() ?? new JsonArray();
        if (junctions.Any(value => value!["slot"]!.Deserialize<int>() == 1))
        {
            throw new InvalidDataException("Ordinary attack preparation cannot replace an explicitly declared Strength junction.");
        }
        foreach (int spell in Enumerable.Range(1, rules.MagicRecords.Length - 1).OrderByDescending(spell => rules.MagicRecords[spell][24]).ThenBy(spell => spell))
        {
            if (rules.MagicRecords[spell][24] == 0 || prepared.Records[row].Junction.MagicJunctions.Take(19).Where((_, slot) => slot != 1).Contains((byte)spell) || junctions.Any(value => value!["action"]!.Deserialize<int>() == spell))
            {
                continue;
            }
            NativeCharacterVitals candidate = prepared.ChangeMagicStock(row, spell, 100 - prepared.GetMagicCount(row, spell), MagicOperationReason.ScriptReward, out MagicStockMutation mutation);
            if (mutation.Rejected != 0 || candidate.GetMagicCount(row, spell) != 100)
            {
                continue;
            }
            candidate = candidate.SetJunctionMagic(row, 1, spell);
            BattleNativePlayerStatRules projected = rules with { MagicJunctions = candidate.Records[row].Junction.MagicJunctions.ToImmutableArray(), Abilities = candidate.Records[row].Junction.Abilities };
            _ = projected.Project(candidate.GetMagicInventory(row), new(candidate.Records[row].HitPointBonus, candidate.Records[row].StatBonuses.ToImmutableArray()));
            JsonObject? stock = stocks.Select(value => value!.AsObject()).SingleOrDefault(value => value["action"]!.Deserialize<int>() == spell);
            if (stock is null)
            {
                if (stocks.Count >= 32)
                {
                    continue;
                }
                stocks.Add(new JsonObject { ["action"] = spell, ["count"] = 100 });
            }
            else
            {
                stock["count"] = 100;
            }
            junctions.Add(new JsonObject { ["slot"] = 1, ["action"] = spell });
            character["magic_junctions"] = junctions;
            recipe["fixture"] = fixture;
            return;
        }
        throw new NotSupportedException("Ordinary attack preparation has no stockable installed Strength junction magic without replacing another junction.");
    }
}
