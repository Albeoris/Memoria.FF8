using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePartyDirectionQueryTests
{
    public static void Run()
    {
        foreach (byte slot in new byte[] { 0, 1, 2 })
        {
            FieldProgram program = Program(slot, 0xA5);
            Host host = new() { Complete = true, Direction = 228 };
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.Variables.WriteByte(FieldBank.Persistent1Byte, (byte)(9 + slot), 2);
            machine.Variables.WriteByte(FieldBank.MapByte, 8, 71);
            machine.Variables.WriteByte(FieldBank.MapByte, 10, 72);
            machine.AdvanceTick();
            Check(host.Queries.Single().Command == new FieldActorQuery(FieldActorQueryKind.Direction, 0), "PGETDIR must resolve the literal script-party slot to its registered entity.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 228 && machine.Variables.ReadByte(FieldBank.MapByte, 8) == 71 && machine.Variables.ReadByte(FieldBank.MapByte, 10) == 72, "PGETDIR must write one unsigned byte through the low bank nibble only.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 20) == 87, "PGETDIR must advance four bytes and continue in the same native script pass.");
        }
        foreach ((byte slot, byte member, bool bound) in new (byte, byte, bool)[] { (3, 2, true), (255, 2, true), (0, 255, true), (0, 1, true), (0, 2, false) })
        {
            FieldProgram program = Program(slot, 0xA8, bound);
            Host host = new() { Complete = true, Direction = 228 };
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, member);
            machine.Variables.WriteByte(FieldBank.MapByte, 9, 41);
            machine.AdvanceTick();
            Check(host.Queries.Count == 0 && machine.Variables.ReadByte(FieldBank.MapByte, 9) == 41 && machine.Variables.ReadByte(FieldBank.MapByte, 20) == 87, "An absent PGETDIR slot/member/entity/model must skip without reading its unsupported destination selector, writing or yielding.");
        }
        UnboundCallerWithRegisteredTarget();
        PendingColdContinuation();
        Console.WriteLine("PASS PGETDIR script-party binding, byte destination, missing-target skip, same-pass completion and cold request ownership");
    }

    private static void UnboundCallerWithRegisteredTarget()
    {
        ushort[] caller = new ushort[32];
        ushort[] target = Enumerable.Repeat((ushort)10, 32).ToArray();
        FieldProgram program = new([0, 0x73, 0xA5, 1, 9, 0x80, 0x50, 20, 87, 0, 0xA1, 7, 0xA0, 2, 0, 0], [caller, target]);
        Host host = new() { Complete = true, Direction = 129 };
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 2);
        machine.AdvanceTick();
        Check(host.Queries.Single().Command == new FieldActorQuery(FieldActorQueryKind.Direction, 1) && machine.Variables.ReadByte(FieldBank.MapByte, 9) == 129, "PGETDIR must resolve character2 to registered entity1 and its model7 even when the caller has no bound model.");
    }

    private static void PendingColdContinuation()
    {
        FieldProgram program = Program(1, 0x52);
        Host host = new() { Direction = 255 };
        FieldMachine original = new(program, host: host);
        original.Initialize();
        original.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 2);
        original.Variables.WriteByte(FieldBank.Persistent0Byte, 9, 12);
        original.Variables.WriteByte(FieldBank.Persistent0Byte, 10, 73);
        original.AdvanceTick();
        FieldHostRequest pending = original.Capture().PendingRequests!.Single();
        Check(pending.ProgramCounter == 5 && original.Variables.ReadByte(FieldBank.Persistent0Byte, 9) == 12, "Pending direction query must preserve its owner PC and destination.");
        Host coldHost = new() { Complete = true, Direction = 255 };
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
        host.Complete = true;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(coldHost.Queries.Single() == pending && original.Variables.ReadByte(FieldBank.Persistent0Byte, 9) == 255 && original.Variables.ReadByte(FieldBank.Persistent0Byte, 10) == 73 && original.Variables.ReadByte(FieldBank.MapByte, 20) == 87, "Cold direction completion changed request identity, unsigned byte result through a word-bank alias or same-pass continuation.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold PGETDIR completion changed VM scheduling state.");
    }

    private static FieldProgram Program(byte slot, byte bank, bool bound = true)
    {
        ushort[] entries = new ushort[32];
        return bound
            ? new([0xA1, 0, 0xA0, 2, 0, 0x73, bank, slot, 9, 0x80, 0x50, 20, 87, 0x12, 0], [entries])
            : new([0, 0x73, bank, slot, 9, 0x80, 0x50, 20, 87, 0x12, 0], [entries]);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Host : IFieldHost
    {
        public bool Complete { get; set; }
        public int Direction { get; set; }
        public List<FieldHostRequest> Queries { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is not FieldActorQuery)
            {
                return new(true);
            }
            Queries.Add(request);
            return new(Complete, Direction);
        }
    }
}
