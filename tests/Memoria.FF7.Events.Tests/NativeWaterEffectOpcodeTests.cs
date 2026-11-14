using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeWaterEffectOpcodeTests
{
    public static void Run()
    {
        byte[] payload = new byte[27];
        payload[2] = 224;
        payload[3] = 255;
        payload[4] = 192;
        payload[5] = 255;
        payload[10] = 224;
        payload[^1] = 73;
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0xA1, 8, 0, 0x28, 30, 8, .. payload, 0x80, 0x50, 9, 87, 0x29, 0x80, 0x50, 10, 91, 0x12, 0], [entries]);
        Host host = new();
        FieldMachine original = new(program, host: host);
        original.Initialize();
        original.AdvanceTick();
        FieldHostRequest request = host.Requests.First(request => request.Command is FieldModelEffectCommand { Wait: false });
        FieldModelEffectCommand command = (FieldModelEffectCommand)request.Command;
        Check(request.ProgramCounter == 3 && command.Operation == 8 && command.Mode == 0 && command.Payload!.SequenceEqual(payload), "KAWAI8 must retain all declared payload bytes, including unused trailing bytes, at its exact owner.");
        Check(original.Variables.ReadByte(FieldBank.MapByte, 9) == 87 && original.Variables.ReadByte(FieldBank.MapByte, 10) == 0, "KAWAI8 must consume length30 in the same pass while KAWIW retains its separate pending owner.");
        FieldHostRequest wait = original.Capture().PendingRequests!.Single();
        Check(wait.ProgramCounter == 37 && wait.Command is FieldModelEffectCommand { Wait: true }, "The persistent water effect lost its following KAWIW owner.");
        Host coldHost = new() { WaitCompleted = true };
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(original.Capture()))!, coldHost);
        host.WaitCompleted = true;
        original.AdvanceTick();
        cold.AdvanceTick();
        Check(coldHost.Requests.Single() == wait && cold.Variables.ReadByte(FieldBank.MapByte, 10) == 91, "Cold KAWIW must poll its original request and continue only on completion.");
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold model-effect wait changed VM scheduling.");
        Console.WriteLine("PASS KAWAI8 complete length30 payload, immediate dispatch and independently owned cold KAWIW");
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
        public bool WaitCompleted { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is FieldActorBind)
            {
                return new(true);
            }
            Requests.Add(request);
            return new(request.Command is not FieldModelEffectCommand { Wait: true } || WaitCompleted);
        }
    }
}
