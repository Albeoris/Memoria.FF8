using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class LiveEvidenceValidation
{
    private static DiagnosticDuelCycleReceipt[] ReadDuelCycles(JsonElement report, JsonElement input, long effect, long action, long? command, bool freshEntry, bool otherCycle)
    {
        bool declared = report.TryGetProperty("duel_cycle", out JsonElement value) && value.ValueKind != JsonValueKind.Null;
        bool originalDeclared = false;
        bool initializedPrelude = false;
        JsonElement original = default;
        JsonElement driver = default;
        if (report.TryGetProperty("initialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.TryGetProperty("driver", out driver))
        {
            originalDeclared = driver.TryGetProperty("duel_cycle", out original) && original.ValueKind != JsonValueKind.Null;
            initializedPrelude = preparation.TryGetProperty("prelude", out JsonElement prelude) && prelude.ValueKind != JsonValueKind.Null;
        }
        DiagnosticDuelCycleReceipt[] cycles = report.TryGetProperty("duel_cycles", out JsonElement receipts) ? receipts.Deserialize<DiagnosticDuelCycleReceipt[]>(Json) ?? throw new InvalidDataException("Duel receipts require an explicit array.") : [];
        if (declared != originalDeclared || declared && !JsonElement.DeepEquals(value, original) || (declared ? cycles.Length != 2 : cycles.Length != 0))
        {
            throw new InvalidDataException("Duel declaration and exactly two initialized cycle receipts must be present together.");
        }
        BattleInput submitted = input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Duel input is absent.");
        if (!declared)
        {
            if (submitted.Ability == BattleAbility.LimitBreak && submitted.ActionId == 11 << 16)
            {
                throw new InvalidDataException("Ordinary Duel requires its source-proved complete cycle protocol.");
            }
            return [];
        }
        if (freshEntry || otherCycle || initializedPrelude || !JsonElement.DeepEquals(input, driver.GetProperty("input")) || Number(driver, "expected_effect") != effect || OptionalNumber(driver, "expected_native_action") != action || OptionalNumber(driver, "expected_native_command") != command)
        {
            throw new InvalidDataException("Duel input and focal native identity must match the initialized ordinary two-use request.");
        }
        DiagnosticDuelCycleDeclaration declaration = value.Deserialize<DiagnosticDuelCycleDeclaration>(Json) ?? throw new InvalidDataException("Duel declaration is empty.");
        declaration.Validate(submitted, checked((ushort)effect), checked((int)action), command is null ? null : checked((byte)command));
        DiagnosticDuelCycleValidation.ValidateChain(cycles[0], cycles[1]);
        BattleDiagnosticLimitSelection policy = driver.GetProperty("limit_selection").Deserialize<BattleDiagnosticLimitSelection>(Json) ?? throw new InvalidDataException("Duel requires initialized ordinary Limit selection bounds.");
        policy.Validate();
        if (policy.MinimumCrisisLevel != 1 || policy.MaximumCrisisLevel != 1)
        {
            throw new InvalidDataException("Duel diagnostics require the initialized bounded crisis1 selection policy.");
        }
        JsonElement[] selections = report.GetProperty("limit_selections").EnumerateArray().ToArray();
        if (selections.Length != cycles.Length)
        {
            throw new InvalidDataException("Duel cycles require exactly the actual ordinary Limit selections.");
        }
        for (int index = 0; index < cycles.Length; index++)
        {
            DiagnosticDuelCycleReceipt cycle = cycles[index];
            if (cycle.Declaration != declaration || !JsonElement.DeepEquals(selections[index], JsonSerializer.SerializeToElement(cycle.Selection, Json)) || cycle.Selection.CrisisSelections.Length == 0 || cycle.Selection.CrisisSelections.Length > policy.MaximumSelections || cycle.Selection.CrisisSelections[^1] < policy.MinimumCrisisLevel || cycle.Selection.CrisisSelections[^1] > policy.MaximumCrisisLevel)
            {
                throw new InvalidDataException("Duel cycle selection differs from the actual initialized Limit selection ledger.");
            }
        }
        return cycles;
    }

    private static void ValidateDuelExecution(DiagnosticDuelCycleReceipt cycle, int index, JsonElement input, JsonElement execution, ushort effect, BattleDiagnosticEffectDispatch[] dispatches, long startCallback, long endCallback)
    {
        BattleInput submitted = input.Deserialize<BattleInput>(Json) ?? throw new InvalidDataException("Duel input is absent.");
        DiagnosticDuelCycleValidation.ValidateReceipt(cycle, submitted, effect, dispatches, startCallback, endCallback);
        if (cycle.Execution != index)
        {
            throw new InvalidDataException("Duel receipt belongs to a different execution.");
        }
        foreach (string boundary in new[] { "before", "after" })
        {
            JsonElement[] states = execution.GetProperty(boundary).EnumerateArray().ToArray();
            JsonElement[] actors = states.Where(state => Text(state, "id") == cycle.ActorId).ToArray();
            JsonElement[] targets = states.Where(state => Text(state, "id") == cycle.TargetId).ToArray();
            if (actors.Length != 1 || targets.Length != 1 || OptionalNumber(actors[0], "native_index") != cycle.ActorIndex || OptionalNumber(targets[0], "native_index") != cycle.TargetIndex)
            {
                throw new InvalidDataException("Duel physical actor and recipient differ from the actual execution ledger.");
            }
        }
    }
}
