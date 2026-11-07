using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class VictoryTerminalHoldValidation
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    public static BattleDiagnosticTerminalHoldDeclaration? ValidateDeclaration(JsonElement driver)
    {
        if (!driver.TryGetProperty("terminal_hold", out JsonElement hold) || hold.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        BattleDiagnosticTerminalHoldDeclaration declaration = hold.Deserialize<BattleDiagnosticTerminalHoldDeclaration>(Json) ?? throw new InvalidDataException("Victory hold declaration is absent.");
        declaration.Validate();
        BattleInput input = driver.GetProperty("input").Deserialize<BattleInput>(Json)!;
        DiagnosticSlotsDeclaration slots = driver.GetProperty("slots_admission").Deserialize<DiagnosticSlotsDeclaration>(Json) ?? throw new InvalidDataException("Victory hold requires destructive Slots admission.");
        DiagnosticFreshEntryDeclaration fresh = driver.GetProperty("fresh_entry").Deserialize<DiagnosticFreshEntryDeclaration>(Json) ?? throw new InvalidDataException("Victory hold requires a fresh single-use entry.");
        fresh.Validate();
        slots.Validate(input, driver.GetProperty("expected_effect").GetUInt16(), driver.GetProperty("expected_native_action").GetInt32(), driver.GetProperty("expected_native_command").GetByte());
        if (declaration.Candidate != BattleOutcome.Victory || slots.MagicAction is not (53 or 56) || declaration.PartyActorIds.Length != 1 || declaration.PartyActorIds[0] != input.ActorId || driver.GetProperty("repairs").GetArrayLength() != 0)
        {
            throw new InvalidDataException("Victory hold requires the exact single-use destructive Slots caster without repairs.");
        }
        return declaration;
    }

    public static void Validate(JsonElement report)
    {
        JsonElement driver = report.GetProperty("initialization").GetProperty("preparation").GetProperty("driver");
        BattleDiagnosticTerminalHoldDeclaration config = ValidateDeclaration(driver) ?? throw new InvalidDataException("Victory hold is not initialized.");
        if (report.GetProperty("fresh_entry").ValueKind != JsonValueKind.Object || !JsonElement.DeepEquals(driver.GetProperty("terminal_hold"), report.GetProperty("declared_terminal_hold")) || report.GetProperty("executions").GetArrayLength() != 1 || report.GetProperty("repairs").GetArrayLength() != 0 || report.GetProperty("declared_repairs").GetArrayLength() != 0)
        {
            throw new InvalidDataException("Victory hold requires exactly one unchanged initialized entry and no repairs.");
        }
        JsonElement execution = report.GetProperty("executions")[0];
        BattleDiagnosticTerminalHoldReceipt[] receipts = report.GetProperty("terminal_holds").Deserialize<BattleDiagnosticTerminalHoldReceipt[]>(Json)!;
        if (receipts.Length != 2 || receipts[0].Kind != "held" || receipts[1].Kind != "released")
        {
            throw new InvalidDataException("Victory hold requires exactly one held/released transition without rearm or expiry.");
        }
        Guid lifecycle = Guid.Parse(execution.GetProperty("start_owners").GetProperty("retained_memory_owner_id").GetString()!);
        BattleDiagnosticTerminalHoldReceipt held = receipts[0];
        BattleDiagnosticTerminalHoldReceipt released = receipts[1];
        if (held.HoldId == Guid.Empty || lifecycle == Guid.Empty || held.Frame < 0 || held.Callback < execution.GetProperty("start_callback").GetInt64() || held.Callback > execution.GetProperty("end_callback").GetInt64() || released.Callback < execution.GetProperty("end_callback").GetInt64() || released.Callback - held.Callback > config.MaximumCallbacks || released.Frame < held.Frame || !released.Drained)
        {
            throw new InvalidDataException("Victory hold changed its native callback, lifetime budget or completed drain.");
        }
        Dictionary<string, JsonElement> before = execution.GetProperty("before").EnumerateArray().ToDictionary(actor => actor.GetProperty("id").GetString()!, StringComparer.Ordinal);
        Dictionary<string, JsonElement> after = execution.GetProperty("after").EnumerateArray().ToDictionary(actor => actor.GetProperty("id").GetString()!, StringComparer.Ordinal);
        foreach (BattleDiagnosticTerminalHoldReceipt receipt in receipts)
        {
            if (receipt.HoldId != held.HoldId || receipt.LifecycleId != lifecycle || receipt.Candidate != BattleOutcome.Victory || receipt.Sequence != 1 || receipt.NativeResult is > 0 || receipt.RestoreCount != 0 || receipt.ResetCount != 0 || receipt.Random is null || receipt.Party is null || !receipt.Party.Select(actor => actor.Id).SequenceEqual(config.PartyActorIds))
            {
                throw new InvalidDataException("Victory hold lost its actual candidate, native owner or uninterrupted lineage.");
            }
            foreach (BattleDiagnosticCombatant actor in receipt.Party)
            {
                if (actor.NativeIndex is not (>= 0 and < 3) || actor.CurrentHp <= 0 || actor.CurrentHp > actor.MaximumHp || (actor.Statuses0 & 5) != 0 || (actor.Statuses1 & 0x80010000) != 0 || !before.TryGetValue(actor.Id, out JsonElement first) || !after.TryGetValue(actor.Id, out JsonElement last) || first.GetProperty("native_index").GetInt32() != actor.NativeIndex || last.GetProperty("native_index").GetInt32() != actor.NativeIndex || receipt.Kind == "released" && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actor, RecordJson.Options), last))
                {
                    throw new InvalidDataException("Victory hold changed its living party owner or release state.");
                }
            }
        }
        JsonElement[] enemies = before.Values.Where(actor => actor.GetProperty("native_index").ValueKind == JsonValueKind.Number && actor.GetProperty("native_index").GetInt32() is >= 3 and < 7).ToArray();
        if (enemies.Length == 0 || enemies.Any(actor => !after.TryGetValue(actor.GetProperty("id").GetString()!, out JsonElement last) || last.GetProperty("native_index").ValueKind == JsonValueKind.Number && (last.GetProperty("statuses0").GetInt32() & 5) == 0 && (last.GetProperty("statuses1").GetUInt32() & 0x10000) == 0))
        {
            throw new InvalidDataException("Victory hold lacks the actual natural enemy KO or ejection outcome.");
        }
    }
}
