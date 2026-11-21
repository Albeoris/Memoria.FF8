using System.Text.Json;
using Memoria.FF7.Events;

internal static class RemotePersistentEntryTests
{
    public static void Run()
    {
        CheckCurrentPersistentBranch();
        CheckUnknownForeignFacts();
        CheckEffectsAndUnsupportedPaths();
        CheckInspectionPurity();
    }

    private static void CheckCurrentPersistentBranch()
    {
        FieldMachine machine = Create([0x16, 0x20, 0, 0, 0x33, 2, 3, 3, 0x33, 1, 0]);
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 641);
        FieldNavigationEntryProof current = machine.ProvePersistentNavigationEntryHasNoEffect(0, 1);
        Check(current.Complete && current.HasNoEffect, "A current global story predicate did not prove its finite no-effect branch.");
        Check(current.Reads.Contains(new(FieldBank.Persistent0Word, 0, true)), "The remote proof omitted its global story dependency.");
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 538);
        CheckRejected(machine, "A changed global story retained a proof whose currently admitted branch publishes controls.");
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 563);
        CheckNoEffect(machine, "The strict native comparison failed at its exact boundary.");
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 562);
        CheckRejected(machine, "The native comparison boundary incorrectly suppressed its admitted effect.");

        foreach (FieldBank bank in new[] { FieldBank.Persistent0Byte, FieldBank.Persistent1Byte, FieldBank.Persistent2Byte, FieldBank.Persistent3Byte, FieldBank.Persistent4Byte })
        {
            FieldMachine flags = Create([0x14, (byte)((int)bank << 4), 8, 1, 0, 3, 0x33, 1, 0]);
            flags.Variables.WriteByte(bank, 8, 0);
            CheckNoEffect(flags, $"Persistent bank {bank} was treated as an unknown foreign-map fact.");
            flags.Variables.WriteByte(bank, 8, 1);
            CheckRejected(flags, $"An updated persistent bank {bank} reused a stale no-effect proof.");
        }
    }

    private static void CheckUnknownForeignFacts()
    {
        FieldMachine map = Create([0x14, 0x50, 8, 1, 0, 3, 0x33, 1, 0]);
        map.Variables.WriteByte(FieldBank.MapByte, 8, 0);
        CheckRejected(map, "A foreign map byte borrowed the current field's zero and hid a possible effect.");
        map.Variables.WriteByte(FieldBank.MapByte, 8, 1);
        CheckRejected(map, "A foreign map byte was treated as authoritative after a local update.");

        FieldMachine previousMap = Create([0x16, 0x60, 17, 0, 0x7A, 1, 0, 3, 0x33, 1, 0]);
        previousMap.Variables.WriteWord(FieldBank.MapWord, 17, 360);
        CheckRejected(previousMap, "A foreign previous-map scratch word selected only the current field's branch.");

        FieldMachine bothReturn = Create([0x14, 0x50, 8, 1, 0, 2, 0, 0]);
        CheckNoEffect(bothReturn, "An unknown foreign map condition with two finite returns was rejected.");

        FieldMachine input = Create([0x30, 0x20, 2, 3, 0x33, 1, 0]);
        input.SetInput(0, 0);
        CheckRejected(input, "A missing current button hid an effect available under another native input.");
        input.SetInput(0x20, 0x20);
        CheckRejected(input, "A pressed current button narrowed the required all-input proof.");
        CheckNoEffect(Create([0x30, 0x20, 2, 4, 0, 0, 0, 0]), "An all-input predicate with finite no-effect outcomes was rejected.");
        CheckRejected(Create([0xCB, 0, 3, 0x33, 1, 0]), "A party-membership alternative with a native effect was suppressed.");
    }

    private static void CheckEffectsAndUnsupportedPaths()
    {
        byte[][] effects =
        [
            [0x40, 0, 1, 0],
            [0x33, 1, 0],
            [0x80, 0x10, 8, 1, 0],
            [0x80, 0x50, 8, 1, 0],
            [0x01, 0, 0x21, 0],
            [0x03, 0, 0x21, 0],
            [0xC1, 0x66, 0x66, 0, 2, 4, 6, 8, 0],
            [0x12, 0],
            [0xFF, 0]
        ];
        foreach (byte[] effect in effects)
        {
            CheckRejected(Create(effect), $"A remote proof accepted host work, a write, a request, a loop or an unsupported opcode {effect[0]:X2}.");
        }
        FieldMachine finite = Create([0x10, 1, 0]);
        CheckNoEffect(finite, "A finite native unconditional jump to RET was rejected.");
        FieldNavigationEntryProof limited = finite.ProvePersistentNavigationEntryHasNoEffect(0, 1, instructionBudget: 1);
        Check(!limited.Complete || !limited.HasNoEffect, "An exhausted bounded proof was reported as a complete no-effect result.");
    }

    private static void CheckInspectionPurity()
    {
        byte[] entry = [0x16, 0x20, 0, 0, 0x33, 2, 3, 3, 0x33, 1, 0];
        byte[] original = (byte[])entry.Clone();
        FieldMachine machine = Create(entry);
        machine.Variables.WriteWord(FieldBank.Persistent0Word, 0, 641);
        machine.Variables.WriteByte(FieldBank.MapByte, 8, 127);
        machine.SetInput(0x20, 0x20, rawHeld: 0x80, rawPressed: 0x80);
        machine.SetMovementInput(1, -1, true);
        byte[] persistent = machine.Variables.CopyPersistent();
        byte[] map = machine.Variables.CopyMap();
        string input = JsonSerializer.Serialize(machine.Input);
        string identity = machine.ProgramIdentity;
        int executed = 0;
        int returned = 0;
        machine.InstructionExecuted = (_, _, _, _) => executed++;
        machine.ScriptReturned = _ => returned++;
        FieldNavigationEntryProof first = machine.ProvePersistentNavigationEntryHasNoEffect(0, 1);
        FieldNavigationEntryProof second = machine.ProvePersistentNavigationEntryHasNoEffect(0, 1);
        Check(first.Complete && first.HasNoEffect && JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second), "The same current global facts produced inconsistent remote entry proofs.");
        Check(!machine.Initialized && machine.Tick == 0 && machine.CycleCursor is null, "Remote inspection initialized or advanced the foreign native VM.");
        Check(executed == 0 && returned == 0, "Remote inspection published native instruction or script-return callbacks.");
        Check(persistent.AsSpan().SequenceEqual(machine.Variables.CopyPersistent()) && map.AsSpan().SequenceEqual(machine.Variables.CopyMap()), "Remote inspection changed native persistent or map variables.");
        Check(input == JsonSerializer.Serialize(machine.Input) && identity == machine.ProgramIdentity && original.AsSpan().SequenceEqual(entry), "Remote inspection changed native input or source bytecode identity.");
    }

    private static FieldMachine Create(byte[] entry)
    {
        // The foreign initialization would overwrite the story if it were accidentally executed.
        byte[] code = [0x80, 0x10, 0, 0, 0, .. entry];
        ushort[] entries = Enumerable.Repeat((ushort)5, 32).ToArray();
        entries[0] = 0;
        return new(new FieldProgram(code, [entries]));
    }

    private static void CheckNoEffect(FieldMachine machine, string message)
    {
        FieldNavigationEntryProof proof = machine.ProvePersistentNavigationEntryHasNoEffect(0, 1);
        Check(proof.Complete && proof.HasNoEffect, message + " " + proof.Reason);
    }

    private static void CheckRejected(FieldMachine machine, string message)
    {
        FieldNavigationEntryProof proof = machine.ProvePersistentNavigationEntryHasNoEffect(0, 1);
        Check(!proof.Complete || !proof.HasNoEffect, message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
