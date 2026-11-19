using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeCustomLightingOpcodeTests
{
    internal static void Run()
    {
        byte[] payload = Enumerable.Range(0, 34).Select(index => (byte)(index * 7)).ToArray();
        payload[0] = 0;
        payload[1] = 83;
        int length = payload.Length + 3;
        int applicationPc = 3 + length + 4 + 3;
        int waitPc = applicationPc + 4 + 4;
        ushort[] entries = new ushort[32];
        FieldProgram program = new([0xA1, 9, 0, 0x28, (byte)length, 7, .. payload, 0x80, 0x50, 9, 87, 0x24, 1, 0, 0x28, 4, 7, 1, 0x80, 0x50, 10, 91, 0x29, 0x80, 0x50, 11, 99, 0x12, 0], [entries]);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        FieldHostRequest initialize = host.Requests.Single(request => request.Command is FieldModelEffectCommand { Wait: false });
        Check(initialize.Entity == 0 && initialize.ProgramCounter == 3 && initialize.Command is FieldModelEffectCommand { Operation: 7, Mode: 0, Wait: false } command && command.Payload!.SequenceEqual(payload), "Custom lighting lost its declared payload or immediate producer owner.");
        Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 87 && machine.Capture().Entities[0].WaitCounter == 1, "Mode0 did not advance its complete declared length before the independent WAIT.");
        machine.AdvanceTick();
        FieldHostRequest apply = host.Requests.Single(request => request.Command is FieldModelEffectCommand { Operation: 7, Mode: 1, Wait: false });
        Check(apply.ProgramCounter == applicationPc && ((FieldModelEffectCommand)apply.Command).Payload!.SequenceEqual(new byte[] { 1 }) && machine.Variables.ReadByte(FieldBank.MapByte, 10) == 91, "Mode1 borrowed cache operands from its following instruction or retained the producer.");
        FieldHostRequest wait = machine.Capture().PendingRequests!.Single();
        Check(wait.ProgramCounter == waitPc && wait.Command is FieldModelEffectCommand { Wait: true }, "Persistent custom lighting lost its separately owned KAWIW.");
        Host coldHost = new() { WaitCompleted = true };
        FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, coldHost);
        host.WaitCompleted = true;
        machine.AdvanceTick();
        cold.AdvanceTick();
        Check(coldHost.Requests.Single() == wait && cold.Variables.ReadByte(FieldBank.MapByte, 11) == 99, "Cold KAWIW changed its exact pending owner or resumed before completion.");
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Custom-lighting producer cold continuation changed VM timing.");
        Console.WriteLine("PASS custom-lighting full byte payload, immediate producer, WAIT and independent persistent KAWIW ownership");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }

    private sealed class Host : IFieldHost
    {
        public bool WaitCompleted { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is not FieldActorBind)
            {
                Requests.Add(request);
            }
            return new(request.Command is not FieldModelEffectCommand { Wait: true } || WaitCompleted);
        }
    }
}
