using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeAtomicControlEntryTests
{
    private sealed record Fixture(FieldProgram Program, FieldMachine Machine, FieldNavigationControlState Controls, byte[] Code, int ProducerEntry);

    internal static void Run()
    {
        byte[] balanced = [0x33, 1, 0x4A, 1, 0x33, 0, 0x4A, 0, 0];
        Fixture plain = Create(balanced);
        Check(Proof(plain).HasNoEffect, "A balanced control pair returning within one native quantum was treated as a persistent authority change.");
        Check(!Proof(plain with { Controls = plain.Controls with { PlayerAnimationStatus = 2 } }).HasNoEffect, "Player unlock's native animation reset was ignored.");
        byte[] conditional = [0x33, 1, 0x4A, 1, 0x14, 0xD0, 0x5B, 0x80, 6, 5, 0x80, 0x50, 10, 1, 0x33, 0, 0x4A, 0, 0];
        Fixture inactive = Create(conditional);
        FieldNavigationEntryProof inert = Proof(inactive);
        Check(inert.Complete && inert.HasNoEffect && inert.Reads.Any(read => read.Bank == FieldBank.Persistent3Byte && read.Operand == 0x5B), "The actual immutable false guard did not preserve its balanced inactive entry.");
        inactive.Machine.Variables.WriteByte(FieldBank.Persistent3Byte, 0x5B, 128);
        Check(!Proof(inactive).HasNoEffect, "The real effectful guard branch was hidden by balanced controls.");
        inactive.Machine.Variables.WriteByte(FieldBank.Persistent3Byte, 0x5B, 0);
        Check(!Proof(inactive, externalWrites: [new(FieldBank.Persistent3Byte, 0x5B, false)]).HasNoEffect, "A mutable external guard publication was mistaken for an immutable inactive branch.");
        foreach (byte[] body in new byte[][]
        {
            [0x33, 1, 0],
            [0x4A, 1, 0],
            [0x33, 1, 0x24, 1, 0, 0x33, 0, 0],
            [0x33, 1, 0x33, 0, 0x4A, 1, 0x4A, 0, 0x33, 1, 0x33, 0, 0x4A, 1, 0x4A, 0, 0],
            [0xA1, 0, 0],
            [0x49, 0, 0, 0, 0]
        })
        {
            Check(!Proof(Create(body)).HasNoEffect, "Unbalanced authority, yielded work, excessive handlers, binding or a menu was certified inert.");
        }
        Check(Proof(Create([0x33, 1, 0x4A, 1, 0x33, 0, 0x4A, 0, 0x4A, 0, 0x4A, 0, 0x4A, 0, 0])).HasNoEffect, "A balanced eight-handler return was rejected at the native quantum boundary.");
        foreach (byte[] producer in new byte[][] { [0x33, 1, 0], [0x4A, 1, 0], [0xA1, 0, 0], [0x49, 0, 0, 0, 0] })
        {
            Check(!Proof(Create(balanced, producer)).HasNoEffect, "Another actual producer inherited exemptions for the selected entry's control PCs.");
        }
        Fixture sameAddresses = Create(balanced);
        FieldVmSnapshot competing = sameAddresses.Machine.Capture();
        competing.Entities[1] = competing.Entities[1] with { ProgramCounter = 2 };
        sameAddresses = sameAddresses with { Machine = FieldMachine.Restore(sameAddresses.Program, competing) };
        Check(!Proof(sameAddresses).HasNoEffect, "The same instruction addresses in another entity inherited the selected owner's exemption.");
        Fixture pending = Create(balanced, [0x33, 1, 0]);
        FieldVmSnapshot pendingVm = pending.Machine.Capture();
        pendingVm.Entities[1] = pendingVm.Entities[1] with { Priority = 6 };
        pendingVm.Entities[1].SavedProgramCounters[7] = 1;
        pendingVm = pendingVm with { NextRequestId = 8, PendingRequests = [new(7, 1, 6, pending.ProducerEntry, new FieldControlCommand(FieldControlKind.PlayerLocked, 1))] };
        pending = pending with { Machine = FieldMachine.Restore(pending.Program, pendingVm) };
        Check(!Proof(pending).HasNoEffect, "A different pending control producer was omitted from the native closure.");
        Console.WriteLine("Atomic control entries restore captured authority within one native quantum without exempting other producers.");
    }

    private static Fixture Create(byte[] body, byte[]? producer = null)
    {
        byte[] code = [0, 0, .. body, .. (producer ?? [])];
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[0][1] = 2;
        int producerEntry = 2 + body.Length;
        if (producer is not null)
        {
            entries[1][1] = (ushort)producerEntry;
        }
        FieldProgram program = new(code, entries);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture();
        vm.Entities[1] = vm.Entities[1] with { ModelBound = true, ProgramCounter = producer is null ? 1 : producerEntry };
        machine = FieldMachine.Restore(program, vm);
        return new(program, machine, new(false, false, 1, 0, []), code, producerEntry);
    }

    private static FieldNavigationEntryProof Proof(Fixture source, IReadOnlyList<FieldVariableRead>? externalWrites = null)
    {
        string before = JsonSerializer.Serialize(source.Machine.Capture());
        byte[] original = source.Code.ToArray();
        FieldNavigationEntryProof proof = source.Machine.ProveNavigationEntryHasNoEffect(0, 1, new(), externalWrites ?? [], [], controls: source.Controls);
        Check(before == JsonSerializer.Serialize(source.Machine.Capture()) && source.Code.SequenceEqual(original), "Read-only control-entry proof changed native execution, variables or script bytes.");
        return proof;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
