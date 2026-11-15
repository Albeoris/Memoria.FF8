using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeWindowNumberOpcodeTests
{
    public static void Run()
    {
        foreach ((short low, short high, int expected) in new (short, short, int)[] { (-1, 0x1234, -1), (0x7FFF, short.MinValue, unchecked((int)0x80007FFF)), (1234, 0, 1234) })
        {
            FieldProgram program = new([0, 0x37, 0x66, 2, 4, 0, 6, 0, 255, 0x80, 0x50, 20, 87, 0x24, 1, 0, 0], [new ushort[32]]);
            Host host = new();
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.Variables.WriteWord(FieldBank.MapWord, 4, low);
            machine.Variables.WriteWord(FieldBank.MapWord, 6, high);
            machine.AdvanceTick();
            FieldHostRequest request = host.Requests.Single();
            Check(request.ProgramCounter == 1 && request.Command == new FieldWindowCommand(FieldWindowOperation.Number, 2, Flags: 255, Parameter: expected), "37 must read both signed16 bank operands before OR/shift packing and retain literal window/unsigned digit bytes.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 20) == 87 && machine.Capture().Entities[0].WaitCounter == 1 && machine.Capture().PendingRequests!.Length == 0, "37 must consume eight bytes and complete in the same unbound script pass.");
            Check(JsonSerializer.Deserialize<FieldHostRequest>(JsonSerializer.Serialize(request)) == request, "The window number command lost its exact owner or32bit value through JSON.");
            FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, new Host());
            machine.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "A completed37 command changed following wait timing after cold continuation.");
        }
        Console.WriteLine("PASS native37 signed word packing, bank selectors, digit width, immediate owner and cold continuation");
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
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(true);
        }
    }
}
