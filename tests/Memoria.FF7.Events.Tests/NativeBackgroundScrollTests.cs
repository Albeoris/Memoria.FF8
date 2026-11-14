using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeBackgroundScrollTests
{
    public static void Run()
    {
        foreach ((byte layer, short x, short y) in new (byte, short, short)[] { (2, short.MinValue, short.MaxValue), (3, 16, -16), (3, -16, 16), (2, 0, 0) })
        {
            Host host = new() { Complete = true };
            FieldMachine machine = new(Program(0, layer, x, y), host: host);
            machine.Initialize();
            machine.AdvanceTick();
            Check(host.Requests.Single().Command == new FieldBackgroundCommand(FieldBackgroundOperation.Scroll, layer, x, y), "BGSCR lost its literal layer or two signed immediate words.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "BGSCR must advance seven bytes and continue without yielding.");
        }
        Host bankedHost = new() { Complete = true };
        FieldMachine banked = new(Program(0x62, 3, 13, 17), host: bankedHost);
        banked.Initialize();
        banked.Variables.WriteWord(FieldBank.MapWord, 13, -321);
        banked.Variables.WriteWord(FieldBank.Persistent0Word, 17, 1234);
        banked.AdvanceTick();
        Check(bankedHost.Requests.Single().Command == new FieldBackgroundCommand(FieldBackgroundOperation.Scroll, 3, -321, 1234), "BGSCR must resolve X and Y through the high and low bank nibbles independently.");
        foreach (byte layer in new byte[] { 0, 1, 4, 255 })
        {
            Host host = new() { Complete = true };
            FieldMachine skipped = new(Program(0x88, layer, -1, -1), host: host);
            skipped.Initialize();
            skipped.AdvanceTick();
            Check(host.Requests.Count == 0 && skipped.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "Unsupported BGSCR selectors must skip both unsupported bank reads, the host and any extra yield.");
        }
        FieldProgram program = Program(0, 3, 16, -16);
        Host pending = new();
        FieldMachine original = new(program, host: pending);
        original.Initialize();
        original.AdvanceTick();
        FieldHostRequest owner = original.Capture().PendingRequests!.Single();
        Check(owner.ProgramCounter == 1 && original.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "Pending BGSCR lost its seven-byte owner.");
        Host coldHost = new() { Complete = true };
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
        pending.Complete = true;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(coldHost.Requests.Single() == owner && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "Cold BGSCR changed request identity or same-pass continuation.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold BGSCR changed VM state.");
        string legacy = JsonSerializer.Serialize(new FieldBackgroundCommand(FieldBackgroundOperation.On, 3, 2));
        Check(!legacy.Contains("State2", StringComparison.Ordinal) && JsonSerializer.Deserialize<FieldBackgroundCommand>(legacy)!.State2 == 0, "Adding the second scroll operand changed the old background command shape/default.");
        Console.WriteLine("PASS BGSCR signed words/bank halves, ignored selectors, seven-byte same-pass completion and cold ownership");
    }

    private static FieldProgram Program(byte bank, byte layer, short x, short y)
    {
        ushort[] entries = new ushort[32];
        return new([0, 0x2D, bank, layer, (byte)x, (byte)(x >> 8), (byte)y, (byte)(y >> 8), 0x80, 0x50, 9, 87, 0x12, 0], [entries]);
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
            Requests.Add(request);
            return new(Complete);
        }
    }
}
