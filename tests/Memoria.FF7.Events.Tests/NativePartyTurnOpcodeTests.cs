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
        Console.WriteLine("PASS PTURA raw operands, same-PC polling, native length/yield and cold typed continuation");
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
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is FieldActorBind)
            {
                return new(true);
            }
            Requests.Add(request);
            return new(Complete);
        }
    }
}
