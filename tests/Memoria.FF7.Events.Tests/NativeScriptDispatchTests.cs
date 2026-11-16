using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeScriptDispatchTests
{
    public static void Run()
    {
        foreach (byte mode in new byte[] { 1, 2, 3 })
        {
            CheckAdmission(mode, queued: false);
            CheckAdmission(mode, queued: true);
            CheckBusy(mode, queuedSlot: false);
            CheckBusy(mode, queuedSlot: true);
        }
        CheckReqewContinuation();
    }

    private static void CheckAdmission(byte mode, bool queued)
    {
        FieldProgram program = Program(mode, [0x24, 0, 0, 0x80, 0x50, 1, 8, 0]);
        FieldMachine seed = new(program);
        seed.Initialize();
        FieldVmSnapshot source = seed.Capture();
        if (queued)
        {
            source.Entities[1] = source.Entities[1] with { Priority = 0 };
            source.Entities[1].SavedProgramCounters[7] = 49;
        }
        FieldMachine observed = FieldMachine.Restore(program, source);
        FieldMachine baseline = FieldMachine.Restore(program, source);
        List<FieldScriptDispatch> dispatches = [];
        observed.ScriptDispatched = dispatches.Add;
        for (int tick = 0; tick < 5; tick++)
        {
            observed.AdvanceTick();
            baseline.AdvanceTick();
            SameState(observed, baseline);
        }
        Check(dispatches.Count == 1 && dispatches.Single() == new FieldScriptDispatch(0, 7, 17, 1, 1, 1, mode, true, queued, 0), "A native request did not emit its exact direct/queued admission identity once.");
        Check(observed.Variables.ReadByte(FieldBank.MapByte, 0) == 99 && observed.Variables.ReadByte(FieldBank.MapByte, 1) == 8, "Dispatch observation changed native request execution or suspended continuation.");
    }

    private static void CheckBusy(byte mode, bool queuedSlot)
    {
        FieldProgram program = Program(mode, [0x24, 200, 0, 0]);
        FieldMachine seed = new(program);
        seed.Initialize();
        FieldVmSnapshot source = seed.Capture();
        source.Entities[1] = source.Entities[1] with { ProgramCounter = 64, Priority = queuedSlot ? 0 : 1, WaitCounter = 200 };
        source.Entities[1].SavedProgramCounters[7] = 49;
        if (queuedSlot)
        {
            source.Entities[1].SavedProgramCounters[1] = 64;
        }
        FieldMachine observed = FieldMachine.Restore(program, source);
        FieldMachine baseline = FieldMachine.Restore(program, source);
        List<FieldScriptDispatch> dispatches = [];
        observed.ScriptDispatched = dispatches.Add;
        for (int tick = 0; tick < 2; tick++)
        {
            observed.AdvanceTick();
            baseline.AdvanceTick();
            SameState(observed, baseline);
        }
        Check(dispatches.Count == (mode == 1 ? 1 : 2), "Busy native request attempts were omitted or a skipped request was retried.");
        for (int index = 0; index < dispatches.Count; index++)
        {
            Check(dispatches[index] == new FieldScriptDispatch(0, 7, 17, 1, 1, 1, mode, false, false, index), "A busy direct/queued-slot request reported an accepted dispatch or changed its source owner.");
        }
        Check(observed.Variables.ReadByte(FieldBank.MapByte, 0) == (mode == 1 ? 99 : 0), "Observing busy requests changed skip-versus-wait timing.");
    }

    private static void CheckReqewContinuation()
    {
        FieldProgram program = Program(3, [0x24, 2, 0, 0]);
        FieldMachine observed = new(program);
        FieldMachine baseline = new(program);
        observed.Initialize();
        baseline.Initialize();
        List<FieldScriptDispatch> first = [];
        observed.ScriptDispatched = first.Add;
        observed.AdvanceTick();
        baseline.AdvanceTick();
        SameState(observed, baseline);
        Check(first.Count == 1 && first[0].Accepted, "REQEW omitted its initial native child admission.");
        List<FieldScriptDispatch> continuation = [];
        observed = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(observed.Capture()))!);
        observed.ScriptDispatched = continuation.Add;
        for (int tick = 0; tick < 6; tick++)
        {
            observed.AdvanceTick();
            baseline.AdvanceTick();
            SameState(observed, baseline);
        }
        Check(continuation.Count == 0, "An already admitted REQEW waiting/completion callback reemitted a native dispatch after cold restoration.");
        Check(observed.Variables.ReadByte(FieldBank.MapByte, 0) == 99 && observed.Capture().Entities[1].SyncStates[1] == FieldSyncState.None, "REQEW observation changed its native completion handshake.");
    }

    private static FieldProgram Program(byte mode, byte[] child)
    {
        byte[] code = new byte[128];
        new byte[] { 0, mode, 1, 0x21, 0x80, 0x50, 0, 99, 0 }.CopyTo(code, 16);
        child.CopyTo(code, 64);
        ushort[][] entries = [Enumerable.Repeat((ushort)16, 32).ToArray(), Enumerable.Repeat((ushort)48, 32).ToArray()];
        entries[1][1] = 64;
        return new(code, entries);
    }

    private static void SameState(FieldMachine observed, FieldMachine baseline)
    {
        Check(JsonSerializer.Serialize(observed.Capture()) == JsonSerializer.Serialize(baseline.Capture()), "Attaching a dispatch observer changed native PCs, priorities, waits, variables or request ownership.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
