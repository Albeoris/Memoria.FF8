using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeMusicChangeTests
{
    public static void Run()
    {
        BankedOperandsAndSamePass();
        ImmediateOperandsAndColdOwner();
        Console.WriteLine("PASS native FD music change: literal track/operation, independent signed word operands, eight-byte same-pass completion and cold ownership");
    }

    private static void BankedOperandsAndSamePass()
    {
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0, 0xFD, 7, 0x64, 0x18, 11, 0xA5, 23, 0x5A, 0x80, 0x50, 70, 123, 0x12, 0], [entries]);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.Variables.WriteWord(FieldBank.MapWord, 11, -32000);
        machine.Variables.WriteWord(FieldBank.Persistent1Word, 23, 12345);
        machine.AdvanceTick();
        Check(host.Requests.Single().Command == new FieldMusicChangeCommand(7, 0x18, -32000, 12345), "FD did not decode literal track/operation and independent banked signed parameters.");
        Check(host.Requests.Single().ProgramCounter == 1 && machine.Variables.ReadByte(FieldBank.MapByte, 70) == 123, "FD did not advance eight bytes and execute the following instruction in the same callback.");
        Check((machine.Capture().PendingRequests ?? []).Length == 0, "Completed FD retained a pending request.");
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, new Host());
        Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(machine.Capture()), "Completed FD cold restore changed VM state.");
    }

    private static void ImmediateOperandsAndColdOwner()
    {
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0, 0xFD, 1, 0, 0x14, 0xFE, 0xFF, 0x80, 0, 0x80, 0x50, 4, 77, 0x12, 0], [entries]);
        Host pending = new(completed: false);
        FieldMachine machine = new(program, host: pending);
        machine.Initialize();
        machine.AdvanceTick();
        FieldVmSnapshot saved = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!;
        FieldHostRequest owner = saved.PendingRequests!.Single();
        Check(owner.Command == new FieldMusicChangeCommand(1, 0x14, -2, 128) && saved.Entities[0].ProgramCounter == 1, "FD lost immediate signed words or its unfinished owner.");
        Host completing = new();
        FieldMachine cold = FieldMachine.Restore(program, saved, completing);
        cold.AdvanceTick();
        Check(completing.Requests.Single().Id == owner.Id && completing.Requests.Single().Command == owner.Command, "Pending FD was not reattached to its original typed request.");
        Check(cold.Variables.ReadByte(FieldBank.MapByte, 4) == 77 && (cold.Capture().PendingRequests ?? []).Length == 0, "Cold FD completion did not retire its owner and continue after eight bytes.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Host(bool completed = true) : IFieldHost
    {
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(completed);
        }
    }
}
