using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeAmbientMessageInputTests
{
    private sealed record Fixture(FieldMachine Machine, FieldProgram Program, byte[] Code, FieldScriptInputDemand Demand, FieldHostRequest Prompt);

    internal static void Run()
    {
        Fixture fixture = Create();
        string before = JsonSerializer.Serialize(fixture.Machine.Capture());
        byte[] code = fixture.Code.ToArray();
        Check(fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand, fixture.Prompt), "A current native message/button/choice prefix was not admitted.");
        Check(before == JsonSerializer.Serialize(fixture.Machine.Capture()) && code.AsSpan().SequenceEqual(fixture.Code), "Prompt inspection modified source native execution, variables or code.");
        Check(!fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand with { AwaitingInput = false }, fixture.Prompt), "An ordinary input handler became a waiting prompt.");
        Check(!fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand with { Entity = 2 }, fixture.Prompt), "Another ambient entity borrowed the message.");
        Check(!fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand with { ProgramCounter = 12 }, fixture.Prompt), "A stale input site borrowed the current prompt.");
        Check(!fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand with { Priority = 6 }, fixture.Prompt), "A nonambient input owner was granted ambient admission.");
        Check(!fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand with { Mask = 1 }, fixture.Prompt), "Another button demand borrowed the message prefix.");
        foreach (FieldHostRequest unrelated in new[]
        {
            fixture.Prompt with { Id = 2 },
            fixture.Prompt with { Entity = 2 },
            fixture.Prompt with { Priority = 7 },
            fixture.Prompt with { ProgramCounter = 37 },
            fixture.Prompt with { Command = new FieldDialogueCommand(0, 8) },
            fixture.Prompt with { Command = new FieldDialogueCommand(0, 7, 0, 1) },
            fixture.Prompt with { Command = new FieldCameraCommand(FieldCameraOperation.Wait) }
        })
        {
            Check(!fixture.Machine.ProveCurrentPromptChoiceInput(fixture.Demand, unrelated), "An unrelated request, choice or host operation borrowed the actual message.");
        }
        foreach (Action<byte[]> change in new Action<byte[]>[]
        {
            bytes => bytes[8] = 0x02,
            bytes => bytes[9] = 2,
            bytes => bytes[10] = 0xC2,
            bytes => bytes[15] = 0x01,
            bytes => bytes[16] = 99,
            bytes => bytes[17] = 0xE2,
            bytes => bytes[32] = 0x73,
            bytes => bytes[32] = 0x01,
            bytes => bytes[64] = 0x73,
            bytes => bytes[65] = 0x10,
            bytes => bytes[67] = 1,
            bytes => bytes[68] = 0,
            bytes => bytes[68] = 0xF2,
            bytes => bytes[69] = 0x03
        })
        {
            Fixture invalid = Create(change);
            Check(!invalid.Machine.ProveCurrentPromptChoiceInput(invalid.Demand, invalid.Prompt), "An unproved producer or input prefix with queries, writes or host work became a choice-only prompt.");
        }
        Fixture occupied = Create(workerPriority: 6);
        Check(!occupied.Machine.ProveCurrentPromptChoiceInput(occupied.Demand, occupied.Prompt), "An already occupied worker was selected through ambient prompt admission.");
        Fixture staleDone = Create(workerSync: FieldSyncState.Done);
        Check(!staleDone.Machine.ProveCurrentPromptChoiceInput(staleDone.Demand, staleDone.Prompt), "A completed previous request was mistaken for a new choice worker admission.");
        Fixture waiting = Create(workerSync: FieldSyncState.Waiting);
        Check(!waiting.Machine.ProveCurrentPromptChoiceInput(waiting.Demand, waiting.Prompt), "An outstanding previous request was mistaken for a new choice worker admission.");
        Fixture operandProducer = Create(operandProducer: true);
        Check(!operandProducer.Machine.ProveCurrentPromptChoiceInput(operandProducer.Demand, operandProducer.Prompt), "REQ-like operand bytes of an earlier window instruction became a message producer.");
        FieldMachine uninitialized = new(fixture.Program);
        Check(!uninitialized.ProveCurrentPromptChoiceInput(fixture.Demand, fixture.Prompt), "An uninitialized native machine acquired prompt admission.");
        Console.WriteLine("Current ambient message/button/choice pairing, native-site rejection and source purity checks passed.");
    }

    private static Fixture Create(Action<byte[]>? change = null, int workerPriority = 7, FieldSyncState workerSync = FieldSyncState.None, bool operandProducer = false)
    {
        byte[] code = new byte[96];
        new byte[] { 0x01, 1, 0xC1, 0x31, 0x20, 0x02, 4, 0x03, 2, 0xC2, 0x12, 7 }.CopyTo(code, 8);
        new byte[] { 0x52, 0, 1, 0, 0x40, 0, 7, 0 }.CopyTo(code, 32);
        new byte[] { 0x80, 0x50, 10, 0, 0x48, 0x05, 0, 9, 0, 1, 10, 0 }.CopyTo(code, 64);
        change?.Invoke(code);
        ushort[][] entries = [new ushort[32], new ushort[32], new ushort[32]];
        entries[0][0] = operandProducer ? (ushort)4 : (ushort)7;
        if (operandProducer)
        {
            code[5] = 0x51;
        }
        entries[1][1] = 32;
        entries[2][2] = 64;
        FieldProgram program = new(code, entries);
        FieldMachine initialized = new(program);
        initialized.Initialize();
        FieldVmSnapshot state = initialized.Capture();
        state.Entities[0] = state.Entities[0] with { ProgramCounter = 11, Priority = 7 };
        state.Entities[1] = state.Entities[1] with { ProgramCounter = 36, Priority = 6 };
        state.Entities[2] = state.Entities[2] with { ProgramCounter = 0, Priority = workerPriority };
        if (workerSync != FieldSyncState.None)
        {
            state.Entities[2].SyncStates[6] = workerSync;
            state.Entities[2].SyncOwners[6] = 0;
        }
        FieldHostRequest prompt = new(1, 1, 6, 36, new FieldDialogueCommand(0, 7));
        FieldMachine machine = FieldMachine.Restore(program, state with { NextRequestId = 3, PendingRequests = [prompt], CycleCursor = null });
        FieldScriptInputDemand demand = machine.ReadCurrentInputDemands().First(item => item.Entity == 0 && item.ProgramCounter == 11);
        return new(machine, program, code, demand, prompt);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
