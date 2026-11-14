using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeMovementAnimationOpcodeTests
{
    public static void Run()
    {
        FieldProgram program = new([0, 0xDC, 255, 255, 0, 0xDC, 6, 1, 1, 0xDC, 7, 255, 2, 0xDC, 19, 255, 255, 0x80, 0x50, 7, 123, 0x24, 1, 0, 0], [new ushort[32]]);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        Check(host.Requests.Count == 4 && host.Requests.Select(request => request.ProgramCounter).SequenceEqual(new[] { 1, 5, 9, 13 }), "DC must consume four bytes and dispatch immediately in the same unbound-entity pass.");
        Check(host.Requests[0].Command is FieldMovementAnimationCommand(255, 0) && host.Requests[1].Command is FieldMovementAnimationCommand(6, 1) && host.Requests[2].Command is FieldMovementAnimationCommand(7, 2) && host.Requests[3].Command is FieldMovementAnimationCommand(19, 255), "DC must read literal unsigned animation/selector bytes and ignore byte2 rather than interpreting bank nibbles.");
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 7) == 123 && machine.Capture().Entities[0].WaitCounter == 1 && machine.Capture().PendingRequests!.Length == 0, "DC must return native zero immediately, without creating an actor binding or pending completion owner.");
        FieldHostRequest restored = JsonSerializer.Deserialize<FieldHostRequest>(JsonSerializer.Serialize(host.Requests[0]))!;
        Check(restored == host.Requests[0], "The native movement-animation request lost its exact command and owner through JSON.");
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, new Host());
        machine.AdvanceTick();
        cold.AdvanceTick();
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "DC's completed writes must not change native wait scheduling after a cold continuation.");
        Console.WriteLine("PASS native DC literal operands, ignored byte, four-byte immediate global owner and cold scheduling");
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
