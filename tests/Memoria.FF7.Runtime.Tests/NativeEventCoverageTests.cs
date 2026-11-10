using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

internal static class NativeEventCoverageTests
{
    public static void Run()
    {
        ushort[] entries = new ushort[32];
        entries[1] = 2;
        FieldProgram program = new([0, 0, 0x10, 1, 0, 0], [entries]);
        FieldMachine original = new(program);
        original.Initialize();
        Check(original.TryRequestScript(0, 1), "The native interaction request was not admitted.");
        FieldVmSnapshot before = original.Capture();
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(before))!);
        List<FieldScriptReturn> returns = [];
        List<FieldScriptReturn> coldReturns = [];
        original.ScriptReturned = returns.Add;
        cold.ScriptReturned = coldReturns.Add;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(returns.SequenceEqual(new[] { new FieldScriptReturn(0, 1, 4, 0) }) && returns.SequenceEqual(coldReturns), "The native return observation lost its entity, priority, PC, timing or cold owner.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Observing native returns changed scheduler state or completion timing.");
        original.AdvanceTick();
        Check(returns.Count == 1, "The ordinary priority-seven idle RET must not duplicate interaction completion.");

        AutoPlayEventCoverageLedger ledger = new();
        ledger.Enter("scene:epoch");
        ledger.PublishCatalog([new("chest", "exposed"), new("locked", "locked", AutoPlayEventCoverageStatus.Deferred, Reason: "Native switch remains disabled.")], true);
        Check(!ledger.CanLeave, "An available chest must block the story exit.");
        ledger.Begin("chest", "before-native-state");
        AutoPlayEventCoverageLedger pending = new(JsonSerializer.Deserialize<AutoPlayEventCoverageState>(JsonSerializer.Serialize(ledger.Capture()))!);
        Check(!pending.CanLeave && pending.Current!.Events.Single(entry => entry.Id == "chest").Status == AutoPlayEventCoverageStatus.Active, "Cold pending coverage falsely completed an interaction.");
        pending.Complete("chest", "after-native-state", "native return");
        Check(pending.CanLeave && !pending.AllEventsCompleted, "An explicitly locked event must remain distinct from fully completed coverage.");
        pending.PublishCatalog([new("chest", "new-native-condition")], true);
        Check(!pending.CanLeave, "A changed native availability witness must reopen its event.");
        AutoPlayEventCoverageState snapshot = pending.Capture();
        snapshot.Current!.Events[0] = snapshot.Current.Events[0] with { Id = "external mutation" };
        Check(pending.Current!.Events.All(entry => entry.Id != "external mutation"), "Coverage capture leaked mutable catalog storage.");
        pending.Enter("next-scene:epoch");
        Check(pending.Capture().History is { Length: 1 } history && history[0].Events.Any(entry => entry.Id == "chest" && entry.Status == AutoPlayEventCoverageStatus.Available), "A scene handoff must retain unresolved coverage without inventing completion.");
        bool rejected = false;
        try
        {
            _ = new AutoPlayEventCoverageLedger(new(new("corrupt", [new("chest", "available", AutoPlayEventCoverageStatus.Completed)])));
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "Coverage restoration accepted completion without native before/after and outcome evidence.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
