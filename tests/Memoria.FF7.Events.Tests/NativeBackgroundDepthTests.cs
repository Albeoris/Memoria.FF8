using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeBackgroundDepthTests
{
    public static void Run()
    {
        foreach ((byte layer, ushort depth) in new (byte, ushort)[] { (2, 0), (2, 65535), (3, 4094), (3, 4095) })
        {
            FieldProgram program = Program(0x08, layer, depth);
            Host host = new() { Complete = true };
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.AdvanceTick();
            Check(host.Requests.Single().Command == new FieldBackgroundCommand(FieldBackgroundOperation.Depth, layer, depth), "BGPDH lost its literal layer or unsigned immediate depth word.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "BGPDH must advance five bytes and continue without a native wait/yield.");
        }
        Host bankedHost = new() { Complete = true };
        FieldMachine banked = new(Program(0x68, 3, 13), host: bankedHost);
        banked.Initialize();
        banked.Variables.WriteWord(FieldBank.MapWord, 13, -1);
        banked.AdvanceTick();
        Check(bankedHost.Requests.Single().Command == new FieldBackgroundCommand(FieldBackgroundOperation.Depth, 3, 65535), "BGPDH must read its one depth word through the high bank nibble and ignore the low nibble.");
        foreach (byte layer in new byte[] { 0, 1, 4, 255 })
        {
            Host host = new() { Complete = true };
            FieldMachine skipped = new(Program(0x80, layer, 65535), host: host);
            skipped.Initialize();
            skipped.AdvanceTick();
            Check(host.Requests.Count == 0 && skipped.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "Unsupported BGPDH layers must skip five bytes without resolving the unsupported operand bank, a host write or a yield.");
        }
        Host pendingHost = new();
        FieldProgram pendingProgram = Program(0, 3, 4094);
        FieldMachine original = new(pendingProgram, host: pendingHost);
        original.Initialize();
        original.AdvanceTick();
        FieldHostRequest owner = original.Capture().PendingRequests!.Single();
        Check(owner.ProgramCounter == 1 && original.Variables.ReadByte(FieldBank.MapByte, 9) == 0, "A pending depth publication lost its owner PC or advanced before completion.");
        Host coldHost = new() { Complete = true };
        FieldMachine cold = FieldMachine.Restore(pendingProgram, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
        pendingHost.Complete = true;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(coldHost.Requests.Single() == owner && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 87, "Cold BGPDH did not poll its original typed request then continue in the same pass.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold BGPDH completion changed VM state.");
        Console.WriteLine("PASS BGPDH native layer, word/bank widths, ignored selectors, five-byte same-pass completion and cold request ownership");
    }

    private static FieldProgram Program(byte bank, byte layer, ushort depth)
    {
        ushort[] entries = new ushort[32];
        return new([0, 0x2C, bank, layer, (byte)depth, (byte)(depth >> 8), 0x80, 0x50, 9, 87, 0x12, 0], [entries]);
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
