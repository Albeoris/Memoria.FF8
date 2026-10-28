using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static void DeclareEnemyPartyKnockoutRearm(JsonObject recipe, BattleNativeAction action, byte selector, IReadOnlyList<byte> party, Func<int, int> maximumHitPoints)
    {
        bool canIncapacitateParty = (action.Statuses0 & 5) != 0 || action.Power > 0 && action.AttackType is 1 or 2 or 22 or 26 or 27;
        if (!canIncapacitateParty || (action.Statuses1 & 0x10000) != 0 || selector is not (0 or 201 or 204))
        {
            return;
        }
        byte[] recipients = party.Where(row => row != 255).Take(selector == 0 ? 1 : 3).ToArray();
        if (recipients.Length == 0 || recipients.Distinct().Count() != recipients.Length)
        {
            throw new InvalidDataException("Enemy repeat preparation requires distinct actual party recipients.");
        }
        JsonObject driver = recipe["driver"]!.AsObject();
        JsonArray repairs = driver["repairs"]?.AsArray() ?? new JsonArray();
        if (driver["repairs"] is null)
        {
            driver["repairs"] = repairs;
        }
        foreach (byte row in recipients)
        {
            string actorId = $"party-{row}";
            int hp = maximumHitPoints(row);
            if (hp is < 1 or > ushort.MaxValue)
            {
                throw new InvalidDataException("Enemy repeat preparation requires each recipient's actual positive maximum HP.");
            }
            JsonObject? repair = repairs.OfType<JsonObject>().SingleOrDefault(candidate => candidate["actor_id"]?.GetValue<string>() == actorId);
            if (repair is null)
            {
                repair = new JsonObject { ["actor_id"] = actorId, ["hit_points"] = hp, ["remove_statuses0"] = 0, ["remove_statuses1"] = 0 };
                repairs.Add(repair);
            }
            repair["hit_points"] = hp;
            repair["revive"] = true;
            if ((action.Statuses0 & 4) != 0)
            {
                repair["remove_statuses0"] = (repair["remove_statuses0"]?.GetValue<int>() ?? 0) | 4;
            }
        }
        driver["terminal_hold"] = new JsonObject
        {
            ["reason"] = "Declared two-execution enemy damage or incapacitation repeat: prospectively hold native defeat until the normal presentation drains and explicit recipient repairs complete.",
            ["party_actor_ids"] = new JsonArray(recipients.Select(row => (JsonNode)JsonValue.Create($"party-{row}")!).ToArray()),
            ["maximum_callbacks"] = 18000
        };
    }
}
