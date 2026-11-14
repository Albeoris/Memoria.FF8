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
        Host applicationHost = new();
        FieldProgram application = new([0xA1, 8, 0, 0x28, 4, 8, 1, 0x80, 0x50, 9, 17, 0x29, 0], [entries]);
        FieldMachine mode1 = new(application, host: applicationHost);
        mode1.Initialize();
        mode1.AdvanceTick();
        FieldHostRequest applicationRequest = applicationHost.Requests.Single(request => request.Command is FieldModelEffectCommand { Wait: false });
        Check(applicationRequest.ProgramCounter == 3 && applicationRequest.Command is FieldModelEffectCommand { Operation: 8, Mode: 1, Payload: [1] } && mode1.Variables.ReadByte(FieldBank.MapByte, 9) == 17, "Mode1 must consume exactly length4 and its one-byte payload without reading cache words from following opcodes.");
        Check(mode1.Capture().PendingRequests!.Single().ProgramCounter == 11, "Mode1's persistent KAWIW must retain its own following PC.");
        byte[] selector9Payload = [0, 0, 0xF9, 0xFF, 0, 0, 0, 0, 128, 0, 0];
        Host sharedHost = new();
        FieldProgram shared = new([0xA1, 8, 0, 0x28, 14, 9, .. selector9Payload, 0x24, 1, 0, 0x28, 4, 9, 1, 0x80, 0x50, 11, 23, 0x29, 0], [entries]);
        FieldMachine selector9 = new(shared, host: sharedHost);
        selector9.Initialize();
        selector9.AdvanceTick();
        Check(sharedHost.Requests.Count(request => request.Command is FieldModelEffectCommand { Wait: false }) == 1 && selector9.Capture().Entities[0].WaitCounter == 1, "The original one-callback WAIT between selector9 initialization and application must allow the cache processing phase.");
        selector9.AdvanceTick();
        FieldHostRequest[] sharedRequests = sharedHost.Requests.Where(request => request.Command is FieldModelEffectCommand { Wait: false }).ToArray();
        Check(sharedRequests.Length == 2 && sharedRequests[0].ProgramCounter == 3 && sharedRequests[0].Command is FieldModelEffectCommand { Operation: 9, Mode: 0 } initialize9 && initialize9.Payload!.SequenceEqual(selector9Payload), "Selector9 must preserve the complete length14 mode0 payload with four signed words and the flag byte.");
        Check(sharedRequests[1].ProgramCounter == 20 && sharedRequests[1].Command is FieldModelEffectCommand { Operation: 9, Mode: 1, Payload: [1] } && selector9.Variables.ReadByte(FieldBank.MapByte, 11) == 23, "Selector9 mode1 must consume exactly length4 and continue at the following opcode.");
        Check(selector9.Capture().PendingRequests!.Single().ProgramCounter == 28, "Selector9 must retain the same persistent KAWIW completion owner.");
        Console.WriteLine("PASS shared KAWAI8/9 payload widths, immediate dispatch and independently owned persistent KAWIW");
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
