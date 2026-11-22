using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeOwnedDialogueInputTests
{
    private sealed record Fixture(byte[] Code, FieldMachine Machine, FieldVmSnapshot Vm, Ff7FieldProcess Process, Ff7AutoPlayActiveEvent Active, Ff7Dialogue Dialogue, FieldScriptInputDemand Demand);

    internal static void Run()
    {
        foreach (byte mask in new byte[] { 0x40, 0x80 })
        {
            Fixture source = Create(mask);
            string before = JsonSerializer.Serialize(new { source.Vm, Machine = source.Machine.Capture(), source.Process, source.Active, source.Code });
            FieldInputState selected = new(Held: mask, Pressed: mask, RawHeld: mask, RawPressed: mask);
            Check(source.Machine.CanCloseDialogueFromCurrentInput(source.Demand, 1, selected), "The full current KEY path did not prove its matching native HUD close.");
            Check(Owned(source), "An actually returned root's owned HUD worker lost its authority.");
            Check(Ff7AutoPlayScriptedInput.SelectInput(source.Demand, new()).Held == mask && Ff7AutoPlayScriptedInput.SelectInput(source.Demand, selected).Held == 0, "Native press preparation failed to preserve or release the demanded key.");
            Check(!source.Machine.CanCloseDialogueFromCurrentInput(source.Demand, 1, new(Held: mask, RawHeld: mask)), "A held key without a real press proved a KEYON branch.");
            Check(!source.Machine.CanCloseDialogueFromCurrentInput(source.Demand, 1, new(RawPressed: 0x20)) && !source.Machine.CanCloseDialogueFromCurrentInput(source.Demand, 1, new(RawPressed: 0x10)), "A native menu branch was confused with a close branch.");
            Check(!source.Machine.CanCloseDialogueFromCurrentInput(source.Demand, 0, selected) && !source.Machine.CanCloseDialogueFromCurrentInput(source.Demand with { ProgramCounter = 18 }, 1, selected), "Another window or unmatched demand borrowed a later close branch.");
            Check(before == JsonSerializer.Serialize(new { source.Vm, Machine = source.Machine.Capture(), source.Process, source.Active, source.Code }), "Read-only input proof changed native source, party or ownership.");
        }
        foreach (Fixture invalid in new[] { Create(0x40, (33, 0)), Create(0x40, (35, 0)), Create(0x40, (34, 0x80)), Create(0x40, (32, 0x49)), Create(0x40, (38, 0x12)), Create(0x40, (32, 0x31), (33, 0x80), (34, 0), (35, 3)) })
        {
            Check(!invalid.Machine.CanCloseDialogueFromCurrentInput(invalid.Demand, 1, new(RawPressed: 0x40)), "A foreign window, material write, menu, cycle or unmatched nested chord acquired a close certificate.");
        }
        Fixture valid = Create(0x40);
        foreach (Fixture invalid in new[]
        {
            valid with { Demand = valid.Demand with { Entity = 0 } },
            valid with { Demand = valid.Demand with { Priority = 5 } },
            valid with { Demand = valid.Demand with { ProgramIdentity = "foreign" } },
            valid with { Active = valid.Active with { Accepted = false } },
            valid with { Active = valid.Active with { ExecutionProvenance = null } },
            valid with { Dialogue = valid.Dialogue with { RequestId = 8 } },
            valid with { Dialogue = valid.Dialogue with { FirstChoice = 1, LastChoice = 2 } },
            valid with { Dialogue = valid.Dialogue with { AwaitingInput = false } },
            valid with { Process = valid.Process with { Windows = [new(1, 0, 0, 100, 50, Flags: 0)] } },
            valid with { Process = valid.Process with { Menu = new(99, 7, 0) } },
            valid with { Vm = valid.Vm with { PendingRequests = [] } },
            valid with { Active = valid.Active with { ExecutionProvenance = valid.Active.ExecutionProvenance! with { Owners = [valid.Active.ExecutionProvenance!.Owners[0], valid.Active.ExecutionProvenance!.Owners[1] with { ParentEntity = 0 }] } } }
        })
        {
            Check(!Owned(invalid), "An unrelated thread, owner, input prompt or pending menu inherited HUD authority.");
        }
        ushort[][] staleEntries = [new ushort[32], new ushort[32], new ushort[32]];
        staleEntries[2][3] = 8;
        FieldMachine stale = FieldMachine.Restore(new(valid.Code, staleEntries), valid.Vm with { Entities = [valid.Vm.Entities[0], valid.Vm.Entities[1], valid.Vm.Entities[2] with { ProgramCounter = 9 }] });
        Check(!stale.CanCloseDialogueFromCurrentInput(valid.Demand, 1, new(RawPressed: 0x40)), "A stale current program counter borrowed a later close path.");
        Check(!Ff7AutoPlayScriptedInput.IsOwnedDialogueInput(valid.Vm, valid.Process, valid.Active, 2, valid.Dialogue, valid.Demand), "Another generation borrowed the actual HUD worker.");
        Console.WriteLine("Owned persistent dialogue input selects only a proved same-window native close path.");
    }

    private static Fixture Create(byte mask, params (int Offset, byte Value)[] changes)
    {
        byte[] code = new byte[70];
        new byte[] { 0x31, 0x20, 0, 7, 0x49, 0, 7, 0, 0x12, 8, 0x31, 0x10, 0, 7, 0x49, 0, 9, 0, 0x12, 18, 0x31, mask, 0, 8, 0x54, 1, 0x52, 1, 0, 0, 0, 0x12, 31 }.CopyTo(code, 8);
        foreach (var change in changes)
        {
            code[change.Offset] = change.Value;
        }
        ushort[][] entries = [new ushort[32], new ushort[32], new ushort[32]];
        entries[2][3] = 8;
        FieldProgram program = new(code, entries);
        FieldEntitySnapshot Entity(int pc, int priority) => new(pc, priority, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(7, 1, 6, 64, new FieldDialogueCommand(1, 10));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, program.Identity, 30, true, new byte[1280], new byte[256], [Entity(0, 7), Entity(64, 6), Entity(8, 6)], NextRequestId: 8, PendingRequests: [request]);
        FieldMachine machine = FieldMachine.Restore(program, vm);
        FieldScriptInputDemand demand = new($"{program.Identity}:2:6:001C", program.Identity, 2, 6, 28, FieldScriptInputKind.Pressed, mask, false, true);
        Ff7Dialogue dialogue = new(7, 1, 10, null, null, 0, AwaitingInput: true, Presentation: new(6, 100, 50));
        Ff7FieldProcess process = new() { PlayerLocked = true, MenuDisabled = true, Windows = [new(1, 0, 0, 100, 50, Flags: 1)], Dialogues = [dialogue] };
        Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 0, 4, Ff7AutoPlayTargetKind.LineTouch, null, 1299, "before", Accepted: true, Returned: true);
        active = active with { ExecutionProvenance = Ff7AutoPlayExecutionOwners.Begin(active, program.Identity, 0, 10) with { RootReturned = true, Owners = [new(1, 6, 6, 64, 0, 1, 4, 11, false), new(2, 6, 3, 8, 1, 6, 66, 12, false)] } };
        return new(code, machine, vm, process, active, dialogue, demand);
    }

    private static bool Owned(Fixture value) => Ff7AutoPlayScriptedInput.IsOwnedDialogueInput(value.Vm, value.Process, value.Active, 1, value.Dialogue, value.Demand);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
