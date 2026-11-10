using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePartyTurnOpcodeTests
{
    public static void Run()
    {
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0xA1, 0, 0, 0x35, 2, 255, 1, 0x80, 0x50, 9, 87, 0x12, 0], [entries]);
        Host host = new();
        FieldMachine original = new(program, host: host);
        original.Initialize();
        original.AdvanceTick();
        FieldHostRequest request = original.Capture().PendingRequests!.Single();
        Check(request.ProgramCounter == 3 && request.Command == new FieldActorFaceParty(2, 255, 1), "PTURA lost its raw slot, byte step count, direction policy or four-byte owner PC.");
        Check(original.Capture().Entities[0].ProgramCounter == 3 && original.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "A pending PTURA advanced past its native owner.");
        Host coldHost = new();
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(host.Requests.Last() == coldHost.Requests.Single(), "Cold PTURA changed the pending typed request identity.");
        host.Complete = true;
        coldHost.Complete = true;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(original.Variables.ReadByte(FieldBank.MapByte, 9) == 87 && original.Capture().Entities[0].ProgramCounter == 11, "Completed PTURA must advance four bytes and continue in the same script pass.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold PTURA completion changed native scheduler state.");
        FieldProgram missing = new([0, 0x35, 0, 8, 2, 0x80, 0x50, 9, 87, 0x12, 0], [entries]);
        Host absentHost = new();
        FieldMachine absent = new(missing, host: absentHost);
        absent.Initialize();
        absent.AdvanceTick();
        Check(absentHost.Requests.Count == 0 && absent.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "A caller without a native model must skip party facing without a host request or extra yield.");
        ImmediatePartyFacing();
        Console.WriteLine("PASS PTURA raw operands, same-PC polling, native length/yield and cold typed continuation");
    }

    private static void ImmediatePartyFacing()
    {
        ushort[] entries = new ushort[32];
        foreach (byte slot in new byte[] { 0, 2, 255 })
        {
            FieldProgram program = new([0xA1, 0, 0, 0x34, slot, 0x80, 0x50, 9, 87, 0x12, 0], [entries]);
            Host host = new();
            FieldMachine original = new(program, host: host);
            original.Initialize();
            original.AdvanceTick();
            FieldHostRequest request = original.Capture().PendingRequests!.Single();
            Check(request.ProgramCounter == 3 && request.Command == new FieldActorFaceParty(slot, 0, 0, Immediate: true), "PDIRA must preserve its literal party-slot byte and two-byte command owner.");
            Host coldHost = new() { Complete = true, Value = 1 };
            FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
            host.Complete = true;
            host.Value = 1;
            original.AdvanceTick();
            cold.AdvanceTick();
            Check(original.Capture().Entities[0].ProgramCounter == 5 && original.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "Valid immediate party facing must advance two bytes and yield exactly once after applying its direction.");
            Check(coldHost.Requests.Single().Id == request.Id && JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold immediate party facing changed its request identity or native yield.");
            original.AdvanceTick();
            Check(original.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "The instruction after immediate party facing did not resume on its next callback.");
            FieldMachine missing = new(program, host: new Host { Complete = true });
            missing.Initialize();
            missing.AdvanceTick();
            Check(missing.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "An absent native party target must skip without the valid-target yield.");
        }
        Host absentHost = new() { Complete = true, Value = 1 };
        FieldMachine absent = new(new FieldProgram([0, 0x34, 0, 0x80, 0x50, 9, 87, 0x12, 0], [entries]), host: absentHost);
        absent.Initialize();
        absent.AdvanceTick();
        Check(absentHost.Requests.Count == 0 && absent.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "An unbound PDIRA caller must skip in the same native pass.");
        Console.WriteLine("PASS PDIRA raw slot, native present/absent target yield and cold typed continuation");
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
        public int Value { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is FieldActorBind)
            {
                return new(true);
            }
            Requests.Add(request);
            return new(Complete, Value);
        }
    }
}
