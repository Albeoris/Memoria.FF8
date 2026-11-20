using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeDirectTransitionProofTests
{
    internal static void Run()
    {
        byte[] map = [0x60, 0x2D, 0x02, 0xDB, 0xFF, 0x43, 0x09, 50, 0, 0];
        byte[] guard = [0x14, 0x10, 20, 1, 0, 11];
        (FieldMachine Machine, FieldProgram Program) Create(byte[]? leftGuard = null, byte[]? rightGuard = null, byte[]? rightMap = null, byte[]? prefix = null, byte[]? producer = null)
        {
            byte[] first = [.. prefix ?? [], .. leftGuard ?? guard, .. map, 0];
            byte[] second = [.. rightGuard ?? guard, .. rightMap ?? map, 0];
            byte[] code = [0, 0, .. first, .. second, .. producer ?? []];
            ushort[][] entries = [new ushort[32], new ushort[32], new ushort[32]];
            entries[1][4] = 2;
            entries[2][4] = checked((ushort)(2 + first.Length));
            FieldProgram program = new(code, entries);
            FieldMachine machine = new(program, host: new RejectHost());
            machine.Initialize();
            machine.Variables.WriteByte(FieldBank.Persistent0Byte, 20, 1);
            machine.Variables.WriteByte(FieldBank.Persistent0Byte, 21, 1);
            if (producer is not null)
            {
                FieldVmSnapshot snapshot = machine.Capture();
                snapshot.Entities[0] = snapshot.Entities[0] with { ProgramCounter = code.Length - producer.Length };
                machine = FieldMachine.Restore(program, snapshot, new RejectHost());
            }
            return (machine, program);
        }
        FieldNavigationEntryProof Prove(FieldMachine machine, int entity = 1) => machine.ProveNavigationEntryDirectTransition(entity, 4, new(), [], [new(1, 4), new(2, 4)]);
        var source = Create();
        string before = JsonSerializer.Serialize(source.Machine.Capture());
        FieldNavigationEntryProof first = Prove(source.Machine);
        FieldNavigationEntryProof sibling = Prove(source.Machine, 2);
        Check(first is { Complete: true, HasNoEffect: false, DirectTransition: not null, DirectGuardFingerprint: not null } && first.DirectGuardFingerprint == sibling.DirectGuardFingerprint, "Equivalent direct transitions at different native owners did not share their exact effect proof.");
        Check(first.DirectTransition == new FieldTransitionCommand(557, -37, 2371, 50, 0), "Direct proof lost full literal native transition operands.");
        Check(before == JsonSerializer.Serialize(source.Machine.Capture()), "Direct transition inspection dispatched a live host or changed native state/RNG/input.");
        foreach (int operand in new[] { 1, 3, 5, 7, 9 })
        {
            byte[] changed = (byte[])map.Clone();
            changed[operand]++;
            var different = Create(rightMap: changed);
            Check(Prove(different.Machine).DirectGuardFingerprint != Prove(different.Machine, 2).DirectGuardFingerprint, "A differing map/X/Y/triangle/direction became the same native effect.");
        }
        byte[] anotherAddress = (byte[])guard.Clone();
        anotherAddress[2] = 21;
        var otherStorage = Create(rightGuard: anotherAddress);
        Check(Prove(otherStorage.Machine).DirectGuardFingerprint != Prove(otherStorage.Machine, 2).DirectGuardFingerprint, "Equal values from different guard storage became the same effect.");
        byte[] anotherBank = (byte[])guard.Clone();
        anotherBank[1] = 0x30;
        var otherBank = Create(rightGuard: anotherBank);
        otherBank.Machine.Variables.WriteByte(FieldBank.Persistent1Byte, 20, 1);
        Check(Prove(otherBank.Machine).DirectGuardFingerprint != Prove(otherBank.Machine, 2).DirectGuardFingerprint, "Equal values from different native banks became the same effect.");
        byte[] bitGuard = [0x14, 0x10, 20, 0, 10, 11];
        var otherOperation = Create(rightGuard: bitGuard);
        Check(Prove(otherOperation.Machine).DirectGuardFingerprint != Prove(otherOperation.Machine, 2).DirectGuardFingerprint, "Different comparison/bit-mask semantics became the same effect.");
        byte[] wordGuard = [0x16, 0x20, 20, 0, 1, 0, 0, 11];
        var otherWidth = Create(rightGuard: wordGuard);
        otherWidth.Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 21, 0);
        Check(Prove(otherWidth.Machine).DirectGuardFingerprint != Prove(otherWidth.Machine, 2).DirectGuardFingerprint, "Guard byte/word widths were erased.");
        source.Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 20, 0);
        Check(!Prove(source.Machine).Complete, "A changed guard taking RET retained its old transition proof.");
        foreach (byte[] prefix in new byte[][]
        {
            [0x80, 0x10, 30, 1],
            [0x01, 2, 0x24],
            [0x24, 1, 0],
            [0x30, 0x20, 0, 4],
            [0x31, 0x20, 0, 4],
            [0x32, 0x20, 0, 4],
            [0x99, 0x05, 6],
            [0xB7, 0x05, 0, 6],
            [0x0A, 0, 1, 2],
            [0x40, 0, 1],
            [0x33, 0],
            [0x11, 0xFF, 0xFF],
            [0xFF]
        })
        {
            Check(!Prove(Create(prefix: prefix).Machine).Complete, "A write/REQ/time/input/query/party/dialogue/control/cyclic/unsupported prefix gained direct-transition authority.");
        }
        var writer = Create(producer: [0x80, 0x10, 20, 0, 0]);
        Check(!Prove(writer.Machine).Complete, "A captured producer could overwrite the supposedly closed guard.");
        var budget = Create();
        Check(!budget.Machine.ProveNavigationEntryDirectTransition(1, 4, new(), [], [], 1).Complete, "An exhausted producer proof became a positive transition certificate.");
        Check(!budget.Machine.ProveNavigationEntryDirectTransition(1, 4, new(), [new(FieldBank.Persistent0Byte, 20, false)], []).Complete, "An externally mutable guard gained transition authority.");
        byte[] truncatedCode = [0, 0, 0x60, 0x2D];
        ushort[][] truncatedEntries = [new ushort[32], new ushort[32]];
        truncatedEntries[1][4] = 2;
        FieldMachine truncated = new(new FieldProgram(truncatedCode, truncatedEntries), host: new RejectHost());
        truncated.Initialize();
        Check(!Prove(truncated).Complete, "A truncated native transition gained authority.");
        Console.WriteLine("PASS strict native direct-transition operands, guard footprint, purity and rejected effect/input/time paths");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RejectHost : IFieldHost
    {
        public FieldHostReply Execute(FieldHostRequest request)
        {
            throw new InvalidOperationException("Direct proof must not dispatch a live host.");
        }
    }
}
