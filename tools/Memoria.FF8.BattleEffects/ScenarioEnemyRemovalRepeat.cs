using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TrySelectEnemyRemovalRepeatParty(BattleNativeAction action, byte selector, IReadOnlyList<NativeCharacterVital> records, IReadOnlyList<byte> currentParty, out byte[]? party, out string? blocker)
    {
        party = null;
        blocker = null;
        if (!IsNativeEnemyEjection(action))
        {
            return true;
        }
        if (selector == 204 && (action.Statuses1 & 0x4000000) != 0 && action.Hit == 255 && action.StatusAttack == 255)
        {
            blocker = "NON_REPEATABLE_NATIVE_OUTCOME: the original full-party selector, accuracy, status chance and bypass/ejection mask remove every active party owner on the first invocation. No living original recipient remains for a second invocation; removed allocations cannot be revived or reconstructed by a party HP repair.";
            return false;
        }
        if (selector != 201)
        {
            blocker = "REMOVAL_REPEAT_PRECONDITION: this original ejection selector lacks a proved second living recipient without reactivating a removed owner.";
            return false;
        }
        IReadOnlyList<byte> preferred = currentParty.Count == 3 && currentParty.Count(row => row != 255) == 1 ? new byte[] { 0, 1, 3 } : currentParty;
        return TrySelectThreeLivingPartyRecords(records, preferred, "REMOVAL_REPEAT_PARTY_PRECONDITION", out party, out blocker);
    }

    public static NativeCharacterVitals PrepareEnemyRemovalRepeatParty(JsonObject recipe, NativeCharacterVitals initial, NativeBattleExperienceDefinition experience, byte[] party, int level)
    {
        NativeCharacterVitals prepared = initial with { CombatParty = party.ToArray() };
        foreach (byte row in party)
        {
            prepared = prepared.WithDiagnosticBattleLevel(row, level, experience);
        }
        JsonObject fixture = recipe["fixture"]!.AsObject();
        JsonObject first = fixture["characters"]![0]!.DeepClone().AsObject();
        JsonArray characters = [];
        foreach (byte row in party)
        {
            JsonObject character = row == 0 ? first : new JsonObject { ["row"] = row, ["character_id"] = prepared.Records[row].CharacterId, ["stocks"] = new JsonArray(), ["guardian_forces"] = new JsonArray() };
            character["level"] = level;
            character["hit_points"] = prepared.GetMagicMaximumHitPoints(row);
            characters.Add(character);
        }
        fixture["combat_party"] = new JsonArray(party.Select(row => (JsonNode)JsonValue.Create(row)!).ToArray());
        fixture["characters"] = characters;
        recipe["driver"]!.AsObject().Remove("repairs");
        recipe["driver"]!.AsObject().Remove("terminal_hold");
        return prepared;
    }

    private static bool IsNativeEnemyEjection(BattleNativeAction action) => action.AttackType is 1 or 2 && (action.Statuses1 & 0x10000) != 0;
}
