using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePartyCameraOpcodeTests
{
    public static void Run()
    {
        foreach (byte slot in new byte[] { 0, 1, 2 })
        {
            Host host = new();
            FieldProgram program = Program(slot, 0xF6);
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.Variables.WriteByte(FieldBank.Persistent1Byte, (byte)(9 + slot), 2);
            short duration = slot == 1 ? (short)-300 : (short)300;
            machine.Variables.WriteWord(FieldBank.MapWord, 4, duration);
            machine.AdvanceTick();
            FieldHostRequest target = host.Requests.Single(request => request.Command is FieldCameraCommand { Operation: FieldCameraOperation.Entity });
            Check(target.ProgramCounter == 1 && target.Entity == 0 && target.Command == new FieldCameraCommand(FieldCameraOperation.Entity, 3, Steps: duration, Entity: 1), "6F must resolve the literal script-party slot to its registered entity, read signed duration through the low bank nibble, and preserve the unbound caller owner.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 20) == 87 && machine.Variables.ReadByte(FieldBank.MapByte, 21) == 0, "6F must complete immediately after six bytes while the following67 owns camera completion.");
            FieldHostRequest wait = machine.Capture().PendingRequests!.Single();
            Check(wait.ProgramCounter == 11 && wait.Command == new FieldCameraCommand(FieldCameraOperation.Wait), "The party camera must retain the shared camera-wait request at its own PC.");
            Host coldHost = new() { WaitCompleted = true };
            FieldMachine cold = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, coldHost);
            host.WaitCompleted = true;
            machine.AdvanceTick();
            cold.AdvanceTick();
            Check(coldHost.Requests.Single() == wait && cold.Variables.ReadByte(FieldBank.MapByte, 21) == 91 && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold camera completion must reuse67's exact owner without reissuing6F or re-resolving party membership.");
        }
        foreach ((byte slot, byte member, bool bound) in new (byte, byte, bool)[] { (3, 2, true), (255, 2, true), (0, 255, true), (0, 1, true), (0, 2, false) })
        {
            Host host = new() { WaitCompleted = true };
            FieldMachine machine = new(Program(slot, 0xF8, bound), host: host);
            machine.Initialize();
            machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, member);
            machine.AdvanceTick();
            Check(host.Requests.All(request => request.Command is not FieldCameraCommand { Operation: FieldCameraOperation.Entity }) && machine.Variables.ReadByte(FieldBank.MapByte, 20) == 87, "Missing party/registration/model must skip6F before evaluating an unsupported duration bank, without issuing a target camera command.");
        }
        Host guestHost = new() { WaitCompleted = true };
        FieldMachine guest = new(Program(0, 0, character: 0), host: guestHost);
        guest.Initialize();
        guest.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        guest.AdvanceTick();
        Check(guestHost.Requests.Any(request => request.Command == new FieldCameraCommand(FieldCameraOperation.Entity, 3, Steps: 4, Entity: 1)), "The party camera must preserve native character0 identity for a crossover guest.");
        Console.WriteLine("PASS native6F party camera resolution, signed bank operand, missing-target skip, immediate dispatch and shared cold67 completion");
    }

    private static FieldProgram Program(byte slot, byte bank, bool bound = true, byte character = 2)
    {
        byte[] caller = [0, 0x6F, bank, 4, 0, slot, 3, 0x80, 0x50, 20, 87, 0x67, 0x80, 0x50, 21, 91, 0x24, 1, 0, 0];
        byte[] target = bound ? [0xA1, 7, 0xA0, character, 0, 0] : [0, 0];
        return new([.. caller, .. target], [new ushort[32], Enumerable.Repeat((ushort)caller.Length, 32).ToArray()]);
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
            if (request.Command is not FieldCameraCommand camera)
            {
                return new(true);
            }
            Requests.Add(request);
            return new(camera.Operation != FieldCameraOperation.Wait || WaitCompleted);
        }
    }
}
