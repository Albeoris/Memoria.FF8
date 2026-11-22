using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeInputCounterProgressTests
{
    private sealed record Fixture(FieldProgram Program, FieldMachine Machine, FieldVmSnapshot Source, byte[] Code, int[] Stages, ushort[] Correct, int ChildEntry, int HintEntry);

    internal static void Run()
    {
        Fixture source = Create();
        string before = JsonSerializer.Serialize(new { source.Source, source.Code });
        FieldScriptInputDemand? selected = source.Machine.ReadCurrentCounterProgressInput(0, 1);
        Check(selected is { Mask: 0x80, Entity: 0, Priority: 1, Kind: FieldScriptInputKind.Pressed, AwaitingInput: true }, "A four-stage native counter chain did not choose its real incrementing branch.");
        Check(before == JsonSerializer.Serialize(new { source.Source, source.Code }) && JsonSerializer.Serialize(source.Machine.Capture()) == JsonSerializer.Serialize(source.Source), "Counter-path analysis changed native variables, input or VM ownership.");
        for (int stage = 1; stage < source.Stages.Length; stage++)
        {
            FieldEntitySnapshot[] owners = source.Source.Entities.ToArray();
            owners[0] = owners[0] with { ProgramCounter = source.Stages[stage] };
            FieldVmSnapshot snapshot = source.Source with { MapVariables = source.Source.MapVariables.ToArray(), Entities = owners };
            snapshot.MapVariables[9] = (byte)stage;
            FieldMachine current = FieldMachine.Restore(source.Program, snapshot);
            Check(current.ReadCurrentCounterProgressInput(0, 1)?.Mask == source.Correct[stage], "A later current counter stage reused an earlier button or forgot remaining progress.");
        }
        Check(source.Machine.ReadCurrentCounterProgressInput(0, 6) is null, "Another priority inherited a native counter path.");
        Check(source.Machine.ReadCurrentCounterProgressInput(1, 7) is null, "An unrelated native owner inherited another thread's progress input.");
        foreach (Action<byte[], int[]> change in new Action<byte[], int[]>[]
        {
            (code, stages) => code[stages[0] + 1] = 0x80,
            (code, stages) => code[stages[0] + 2] = 2,
            (code, stages) => code[Find(code, 0x95, 8) + 1] = 1,
            (code, stages) => code[Find(code, 0x95, 8) + 2] = 10,
            (code, stages) => code[Find(code, 0x95, 8)] = 0x97,
            (code, stages) => code[Find(code, 0x03, stages[0]) + 1] = 0,
            (code, stages) => code[Find(code, 0x10, stages[0]) + 1]++,
            (code, stages) => code[stages[0]] = 0x80
        })
        {
            Fixture invalid = Create(change);
            Check(invalid.Machine.ReadCurrentCounterProgressInput(0, 1) is null, "Conflicting keys, wrong storage, decrement or changed call outcome received counter progress advice.");
        }
        Fixture wrongValue = Create();
        wrongValue.Machine.Variables.WriteByte(FieldBank.MapByte, 9, 1);
        Check(wrongValue.Machine.ReadCurrentCounterProgressInput(0, 1) is null, "A counter inconsistent with its remaining equality target received advice.");
        wrongValue.Machine.Variables.WriteByte(FieldBank.MapByte, 9, 254);
        Check(wrongValue.Machine.ReadCurrentCounterProgressInput(0, 1) is null, "A wrapping byte counter received monotone progress advice.");
        Check(Create(child: [0x80, 0x50, 9, 0, 0]).Machine.ReadCurrentCounterProgressInput(0, 1) is null, "A synchronous child resetting the counter was ignored.");
        Check(Create(child: [0x14, 0x50, 9, 0, 0, 2, 0, 0]).Machine.ReadCurrentCounterProgressInput(0, 1) is null, "A conditional child borrowed unconditional presentation authority.");
        Fixture presentation = Create(child: [0xAF, 9, 1, 0x24, 8, 0, 0xF1, 0, 3, 0, 0, 0xAC, 0], hint: [0x40, 1, 1, 0]);
        Check(presentation.Machine.ReadCurrentCounterProgressInput(0, 1)?.Mask == 0x80, "Finite literal animation/audio and an independent asynchronous hint prevented native counter progress.");
        Check(Create(child: [0xF1, 0x10, 3, 0, 0, 0]).Machine.ReadCurrentCounterProgressInput(0, 1) is null, "A banked audio operand borrowed literal presentation authority.");
        Check(Create(child: [0x48, 0, 1, 1, 2, 0]).Machine.ReadCurrentCounterProgressInput(0, 1) is null, "A child decision was mistaken for deterministic presentation.");
        Check(Create(hint: [0x80, 0x50, 9, 0, 0]).Machine.ReadCurrentCounterProgressInput(0, 1) is null, "An asynchronous hint changing the counter acquired presentation authority.");
        CheckCurrentPollingCuts();
        Console.WriteLine("Native counter-input advice requires a full same-owner monotone stage and identical finite child effects.");
    }

    private static void CheckCurrentPollingCuts()
    {
        Fixture source = Create(child: [0]);
        int pc = source.Stages[0];
        for (int cut = 0; cut < 4; cut++)
        {
            FieldEntitySnapshot[] owners = source.Source.Entities.ToArray();
            owners[0] = owners[0] with { ProgramCounter = pc };
            FieldMachine machine = FieldMachine.Restore(source.Program, source.Source with { Entities = owners });
            string before = JsonSerializer.Serialize(machine.Capture());
            FieldScriptInputDemand? proof = machine.ReadCurrentCounterProgressInput(0, 1);
            Check(proof is { Mask: 0x80 }, "A current KEY cut lost the complete enclosing counter stage.");
            Check(JsonSerializer.Serialize(machine.Capture()) == before, "Canonical polling-head analysis advanced native execution.");
            Check((proof!.DelayTicks > 0) == (cut > 1), "The counter proof did not distinguish a reachable arm from an arm beyond the native backward yield.");
            Ff7AutoplayInput input = SelectCurrentCounterInput(machine, proof);
            if (cut > 1)
            {
                Check(input.Held == 0, "A correct but already passed KEY was pressed before its native backward yield.");
                Apply(machine, input);
                machine.AdvanceTick();
                Check(machine.Capture().Entities[0].ProgramCounter == source.Stages[0] && machine.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "Neutral preparation did not execute the real polling yield without taking a handler.");
                proof = machine.ReadCurrentCounterProgressInput(0, 1);
                Check(proof is { Mask: 0x80, DelayTicks: 0 }, "A real returned polling head retained stale preparation delay.");
                input = SelectCurrentCounterInput(machine, proof!);
            }
            Check(input.Held == 0x80, "The actual current stage did not emit its reachable incrementing input.");
            Apply(machine, input);
            Check(machine.Input.RawPressed == 0x80, "The counter handler did not receive a fresh native raw press edge.");
            machine.AdvanceTick();
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 1, "The certified actual native handler did not increment its counter exactly once.");
            pc += 3 + source.Code[pc + 3];
        }

        FieldMachine held = FieldMachine.Restore(source.Program, source.Source);
        held.SetInput(0x80, 0, 0x80, 0);
        FieldScriptInputDemand heldProof = held.ReadCurrentCounterProgressInput(0, 1)!;
        Ff7AutoplayInput release = SelectCurrentCounterInput(held, heldProof);
        Check(release.Held == 0, "A held correct key was mistaken for a new raw press.");
        Apply(held, release);
        held.AdvanceTick();
        Check(held.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "Release preparation fabricated counter progress.");
        FieldScriptInputDemand freshProof = held.ReadCurrentCounterProgressInput(0, 1)!;
        Ff7AutoplayInput fresh = SelectCurrentCounterInput(held, freshProof);
        Check(fresh.Held == 0x80, "Releasing a held key did not prepare its next native press.");
        Apply(held, fresh);
        held.AdvanceTick();
        Check(held.Variables.ReadByte(FieldBank.MapByte, 9) == 1, "The prepared fresh raw press failed to increment once.");

        List<byte> slicedCode = source.Code[..8].ToList();
        for (int jump = 0; jump < 7; jump++)
        {
            slicedCode.AddRange([0x10, 1]);
        }
        slicedCode.AddRange(source.Code[8..]);
        ushort[][] slicedEntries = [new ushort[32], new ushort[32], new ushort[32]];
        slicedEntries[0][1] = 8;
        slicedEntries[1][1] = checked((ushort)(source.ChildEntry + 14));
        slicedEntries[2][1] = checked((ushort)(source.HintEntry + 14));
        FieldProgram slicedProgram = new(slicedCode.ToArray(), slicedEntries);
        FieldMachine sliced = FieldMachine.Restore(slicedProgram, source.Source with { ProgramIdentity = slicedProgram.Identity });
        sliced.AdvanceTick();
        int secondKey = source.Stages[0] + 14 + 3 + source.Code[source.Stages[0] + 3];
        Check(sliced.Capture().Entities[0].ProgramCounter == secondKey && sliced.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "Seven real jumps and an unmatched KEY did not expose the native eight-handler slice boundary.");
        FieldScriptInputDemand? slicedProof = sliced.ReadCurrentCounterProgressInput(0, 1);
        Check(slicedProof is { Mask: 0x80, DelayTicks: 0 }, "A current KEY reached by the real handler slice lost its reachable incrementing arm.");
        Apply(sliced, SelectCurrentCounterInput(sliced, slicedProof!));
        sliced.AdvanceTick();
        Check(sliced.Variables.ReadByte(FieldBank.MapByte, 9) == 1, "The native partial-cascade slice could not advance its counter exactly once.");
    }

    private static Ff7AutoplayInput SelectCurrentCounterInput(FieldMachine machine, FieldScriptInputDemand proof)
    {
        FieldScriptInputDemand observed = machine.ReadCurrentInputDemands().Single(demand => demand.Identity == proof.Identity && demand.AwaitingInput);
        return Ff7AutoPlayScriptedInput.SelectInput(observed with { DelayTicks = Math.Max(observed.DelayTicks, proof.DelayTicks) }, machine.Input);
    }

    private static void Apply(FieldMachine machine, Ff7AutoplayInput input)
    {
        ushort pressed = (ushort)(input.Held & ~machine.Input.Held);
        ushort rawPressed = (ushort)(input.Held & ~machine.Input.RawHeld);
        machine.SetInput(input.Held, pressed, input.Held, rawPressed);
    }

    private static Fixture Create(Action<byte[], int[]>? change = null, byte[]? child = null, byte[]? hint = null)
    {
        List<byte> code = Enumerable.Repeat((byte)0, 8).ToList();
        int[] stages = new int[4];
        ushort[] correct = [0x80, 0x20, 0x40, 0x10];
        byte[] buttons = [0x20, 0x80, 0x10, 0x40];
        for (int stage = 0; stage < stages.Length; stage++)
        {
            stages[stage] = code.Count;
            List<int> rejoins = [];
            foreach (byte button in buttons)
            {
                int key = code.Count;
                code.AddRange([0x31, button, 0, 0]);
                if (button == correct[stage])
                {
                    code.AddRange([0x95, 0x05, 9]);
                }
                code.AddRange([0x03, 1, 0xC1]);
                rejoins.Add(code.Count);
                code.AddRange([0x10, 0]);
                code[key + 3] = checked((byte)(code.Count - key - 3));
            }
            int back = code.Count;
            code.AddRange([0x12, checked((byte)(back - stages[stage]))]);
            int join = code.Count;
            foreach (int jump in rejoins)
            {
                code[jump + 1] = checked((byte)(join - jump - 1));
            }
            code.AddRange([0x54, 1, 0x24, 1, 0]);
            if (hint is not null && stage < stages.Length - 1)
            {
                code.AddRange([0x01, 2, 0xC1]);
            }
        }
        code.AddRange([0x14, 0x50, 9, 4, 0, 2, 0, 0]);
        int childEntry = code.Count;
        code.AddRange(child ?? [0xA2, 1, 1, 0]);
        int hintEntry = code.Count;
        code.AddRange(hint ?? [0]);
        byte[] bytes = code.ToArray();
        change?.Invoke(bytes, stages);
        ushort[][] entries = [new ushort[32], new ushort[32], new ushort[32]];
        entries[0][1] = 8;
        entries[1][1] = checked((ushort)childEntry);
        entries[2][1] = checked((ushort)hintEntry);
        FieldProgram program = new(bytes, entries);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture();
        vm.Entities[0] = vm.Entities[0] with { Priority = 1, ProgramCounter = 8 };
        vm.Entities[0].SavedProgramCounters[7] = 1;
        machine = FieldMachine.Restore(program, vm);
        return new(program, machine, vm, bytes, stages, correct, childEntry, hintEntry);
    }

    private static int Find(byte[] code, byte opcode, int start) => Array.IndexOf(code, opcode, start);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
