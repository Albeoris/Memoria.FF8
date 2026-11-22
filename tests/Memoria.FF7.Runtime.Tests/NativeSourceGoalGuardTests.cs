using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeSourceGoalGuardTests
{
    internal static void Run()
    {
        byte[] code = [0x14, 0x50, 9, 0, 0, 8, 0x82, 0x30, 86, 4, 0x03, 1, 1, 0, 0xC1, 0x66, 0x66, 0, 20, 22, 24, 26, 0];
        ushort[][] entries = [new ushort[32], Enumerable.Repeat((ushort)14, 32).ToArray()];
        FieldProgram program = new(code, entries);
        FieldMachine machine = new(program);
        string native = JsonSerializer.Serialize(new { Persistent = machine.Variables.CopyPersistent(), Map = machine.Variables.CopyMap(), machine.Input });
        string? guard = machine.ReadEntryGuardFingerprint(0, 1);
        Check(guard is not null, "A finite branch prefix before a real literal bit mutation was not certified.");
        Check(JsonSerializer.Serialize(new { Persistent = machine.Variables.CopyPersistent(), Map = machine.Variables.CopyMap(), machine.Input }) == native, "Reading an entry guard changed native variables or input.");
        machine.Variables.WriteByte(FieldBank.MapByte, 14, 7);
        Check(machine.ReadEntryGuardFingerprint(0, 1) == guard, "A child-only presentation input changed the entry guard.");
        machine.Variables.WriteByte(FieldBank.MapByte, 9, 1);
        Check(machine.ReadEntryGuardFingerprint(0, 1) != guard, "A changed map branch dependency retained its guard.");
        machine.Variables.WriteByte(FieldBank.MapByte, 9, 0);
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 86, 16);
        Check(machine.ReadEntryGuardFingerprint(0, 1) is null, "An already satisfied bit mutation qualified as a new material boundary.");
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 86, 1);
        Check(machine.ReadEntryGuardFingerprint(0, 1) != guard, "A changed persistent dependency retained its guard.");
        foreach (byte[] prefix in new byte[][] { [0x03, 1, 1], [0x30, 0x80, 0, 4], [0x12, 0], [0xC1, 0x66, 0x66, 1, 20, 22, 24, 26] })
        {
            FieldMachine unsupported = new(new FieldProgram([.. prefix, .. code], entries));
            Check(unsupported.ReadEntryGuardFingerprint(0, 1) is null, "An unknown, input-dependent, host-query or call-first prefix acquired a guard certificate.");
        }

        Ff7AutoPlayTarget original = new("line", 0, 1, Ff7AutoPlayTargetKind.LineTouch, "initial-full", false, default) { EntryGuardFingerprint = guard };
        Ff7AutoPlayObservation observation = new("visit", program.Identity, 3, [original], true, true) { NavigationKey = "component" };
        Ff7AutoPlaySourceGoal goal = Ff7AutoPlayPolicy.CreateSourceGoal(observation, original, 0);
        goal = goal with { CurrentConditionFingerprint = "planned-full" };
        Ff7AutoPlayTarget moved = original with { ConditionFingerprint = "contact-full" };
        Ff7AutoPlayObservation contact = observation with { Events = [moved] };
        Check(Ff7AutoPlayPolicy.MatchesSourceGoal(goal, contact, moved), "Presentation-only position drift invalidated a proved pending guard.");
        Check(!Ff7AutoPlayPolicy.MatchesSourceGoal(goal, contact with { Events = [moved with { EntryGuardFingerprint = "changed" }] }, moved with { EntryGuardFingerprint = "changed" }), "A changed actual guard retained source intent.");
        Check(!Ff7AutoPlayPolicy.MatchesSourceGoal(goal with { EntryGuardFingerprint = null }, contact, moved), "An old save without a guard certificate bypassed its full fingerprint.");
        Ff7AutoPlayActiveEvent active = new("visit", original.Id, moved.ConditionFingerprint, 3, 0, 1, original.Kind, null, 0, "before", Accepted: true, Returned: true, NavigationKey: "component", CompletionRecorded: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, program.Identity, 0, 5) with { RootReturned = true } };
        Ff7AutoPlaySourceGoal? bound = Ff7AutoPlayPolicy.BindSourceGoalAdmission(goal, contact, moved, active, 0);
        Check(bound is { AcceptedEntry: 0, CurrentConditionFingerprint: "contact-full", ConditionFingerprint: "initial-full" }, "Real admission did not retain initial intent and bind the actual full condition separately.");
        Ff7AutoPlayState state = new() { SourceGoal = bound };
        string coverage = JsonSerializer.Serialize(state.Coverage);
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(state, active with { ConditionFingerprint = "initial-full" }), "The original presentation fingerprint received credit for a different actual admission.");
        Check(!Ff7AutoPlayPolicy.CompleteSourceGoal(state, active with { ConditionFingerprint = "planned-full" }), "A pre-movement planned fingerprint received credit for the later actual contact.");
        Check(Ff7AutoPlayPolicy.CompleteSourceGoal(state, active), "The exact authenticated current source could not complete its pending intent.");
        Check(JsonSerializer.Serialize(state.Coverage) == coverage, "Source intent completion rewrote native event coverage.");
        foreach (Ff7AutoPlayActiveEvent invalid in new[] { active with { Accepted = false }, active with { Entity = 1 }, active with { ConditionFingerprint = "foreign" }, active with { ExecutionProvenance = null } })
        {
            Check(Ff7AutoPlayPolicy.BindSourceGoalAdmission(goal, contact, moved, invalid, 0) is null, "An unauthenticated or foreign admission rebound source conditions.");
        }
        Check(Ff7AutoPlayPolicy.BindSourceGoalAdmission(goal, contact, moved, active, 1) is null, "A foreign entry rebound source conditions.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
