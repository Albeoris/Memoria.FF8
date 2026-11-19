using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePartyApproachOpcodeTests
{
    internal static void Run()
    {
        foreach (byte slot in new byte[] { 0, 1, 2, 200 })
        {
            byte[] code = new byte[24];
            new byte[] { 0, 0x2A, slot, 0x80, 0x50, 8, 7, 0x5F, 0x12, 0 }.CopyTo(code, 0);
            code[21] = 0x12;
            FieldProgram program = new(code, [new ushort[32], Enumerable.Repeat((ushort)20, 32).ToArray(), Enumerable.Repeat((ushort)20, 32).ToArray()]);
            Host host = new();
            FieldMachine seed = new(program, host: host);
            seed.Initialize();
            FieldVmSnapshot snapshot = seed.Capture();
            snapshot.Entities[0] = snapshot.Entities[0] with { ModelBound = true, PartyCharacter = 0 };
            snapshot.Entities[1] = snapshot.Entities[1] with { ModelBound = true, PartyCharacter = 3 };
            snapshot.Entities[2] = snapshot.Entities[2] with { ModelBound = true, PartyCharacter = 1 };
            FieldMachine machine = FieldMachine.Restore(program, snapshot, host);
            byte address = (byte)(9 + slot);
            machine.Variables.WriteByte(FieldBank.Persistent1Byte, address, 3);
            machine.AdvanceTick();
            FieldHostRequest pending = machine.Capture().PendingRequests!.Single();
            Check(pending.Entity == 0 && pending.Priority == 7 && pending.ProgramCounter == 1 && pending.Command is FieldActorMove { TargetEntity: 1, TargetPartySlot: var actual } && actual == slot && machine.Capture().Entities[0].ProgramCounter == 1, "PMOVA lost its unsigned party-slot byte, current character/entity mapping, same-PC native move ownership or initial yield.");
            machine.Variables.WriteByte(FieldBank.Persistent1Byte, address, 1);
            FieldVmSnapshot saved = machine.Capture();
            Host coldHost = new() { Complete = true };
            FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(saved))!, coldHost);
            host.Complete = true;
            machine.AdvanceTick();
            cold.AdvanceTick();
            Check(host.Requests.Last().Id == pending.Id && coldHost.Requests.Single().Id == pending.Id && machine.Variables.ReadByte(FieldBank.MapByte, 8) == 7 && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "PMOVA cold completion changed its pending lifetime or did not advance two bytes in the normal native pass.");
            foreach (bool unboundCaller in new[] { false, true })
            {
                Host absentHost = new();
                FieldVmSnapshot absent = snapshot with { Entities = snapshot.Entities.ToArray() };
                absent.Entities[unboundCaller ? 0 : 1] = absent.Entities[unboundCaller ? 0 : 1] with { ModelBound = false, PartyCharacter = null };
                FieldMachine skipped = FieldMachine.Restore(program, absent, absentHost);
                skipped.Variables.WriteByte(FieldBank.Persistent1Byte, address, 3);
                skipped.AdvanceTick();
                Check(absentHost.Requests.Count == 0 && skipped.Variables.ReadByte(FieldBank.MapByte, 8) == 7, "An unbound PMOVA caller/target created a motion instead of its native no-op continuation.");
            }
            Host emptyHost = new();
            FieldMachine empty = FieldMachine.Restore(program, snapshot, emptyHost);
            empty.Variables.WriteByte(FieldBank.Persistent1Byte, address, 255);
            empty.AdvanceTick();
            Check(emptyHost.Requests.Count == 0 && empty.Variables.ReadByte(FieldBank.MapByte, 8) == 7, "An empty native party slot blocked or fabricated a movement target.");
        }
        Console.WriteLine("PASS PMOVA unsigned party alias, native pending owner, missing bindings and cold two-byte continuation");
    }

    private sealed class Host : IFieldHost
    {
        internal bool Complete { get; set; }
        internal List<FieldHostRequest> Requests { get; } = [];
        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(Complete);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
