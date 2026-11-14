using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeMusicPlayingQueryTests
{
    public static void Run()
    {
        foreach ((byte bank, byte address, int result) in new (byte, byte, int)[] { (5, 9, 0), (6, 17, 1), (1, 25, -7), (4, 31, 256) })
        {
            ushort[] entries = new ushort[32];
            FieldProgram program = new([0, 0xFE, (byte)(0xA0 | bank), address, 0x80, 0x50, 70, 123, 0x12, 0], [entries]);
            Host host = new(result);
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.Variables.WriteByte((FieldBank)bank, address, 0x55);
            machine.Variables.WriteByte((FieldBank)bank, (byte)(address + 1), 0xA5);
            machine.AdvanceTick();
            Check(machine.Variables.ReadByte((FieldBank)bank, address) == (result == 0 ? 0 : 1), "FE did not write its normalized native Boolean to the low-nibble selected bank/address.");
            Check(machine.Variables.ReadByte((FieldBank)bank, (byte)(address + 1)) == 0xA5, "FE overwrote the adjacent byte of a word-selected destination.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 70) == 123, "FE did not continue the following instruction in the same callback.");
            Check(host.Requests.Count == 1 && host.Requests[0].ProgramCounter == 1 && host.Requests[0].Command is FieldMusicPlayingQuery, "FE did not publish one typed music query at its native three-byte owner.");
            Check((machine.Capture().PendingRequests ?? []).Length == 0, "A synchronous FE query retained an unfinished owner.");
            FieldVmSnapshot saved = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!;
            FieldMachine cold = FieldMachine.Restore(program, saved, new Host(result));
            Check(JsonSerializer.Serialize(cold.Capture()) == JsonSerializer.Serialize(machine.Capture()), "Cold FE continuation changed completed query state.");
        }
        PendingQueryKeepsOwner();
        Console.WriteLine("PASS native FE query: bank/address, byte width, normalized Boolean, three-byte same-pass completion and cold ownership");
    }

    private static void PendingQueryKeepsOwner()
    {
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0, 0xFE, 5, 3, 0x80, 0x50, 4, 77, 0x12, 0], [entries]);
        Host pending = new(1, completed: false);
        FieldMachine machine = new(program, host: pending);
        machine.Initialize();
        machine.AdvanceTick();
        FieldVmSnapshot saved = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!;
        FieldHostRequest owner = saved.PendingRequests!.Single();
        Check(saved.Entities[0].ProgramCounter == 1 && owner.Command is FieldMusicPlayingQuery, "An unfinished music query lost its instruction owner.");
        Host completing = new(0);
        FieldMachine cold = FieldMachine.Restore(program, saved, completing);
        cold.AdvanceTick();
        Check(completing.Requests.Single().Id == owner.Id && cold.Variables.ReadByte(FieldBank.MapByte, 4) == 77, "Cold pending FE did not poll the original request and continue after completion.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Host(int value, bool completed = true) : IFieldHost
    {
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(completed, value);
        }
    }
}
