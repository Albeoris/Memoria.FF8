using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class NativeWordTalkRadiusTests
{
    public static void Run()
    {
        foreach ((short operand, short scale, int expected) in new (short, short, int)[] { (1600, 640, 2000), (-1, 513, 65535), (-1, 1, 0), (short.MinValue, 512, 32768), (1, -513, 65535), (short.MinValue, short.MinValue, 0), (short.MaxValue, short.MaxValue, 65408) })
        {
            FieldProgram program = Program([0xA1, 0, 0, 0xD6, 0xA0, unchecked((byte)operand), unchecked((byte)(operand >> 8)), 0x80, 0x50, 9, 77, 0x12, 0], scale);
            Host host = new();
            FieldMachine machine = new(program, host: host);
            machine.Initialize();
            machine.AdvanceTick();
            FieldHostRequest request = host.Requests.Single(item => item.Command is FieldActorProperty);
            Check(request.Entity == 0 && request.Command == new FieldActorProperty(FieldActorPropertyKind.TalkRadius, expected), "D6 lost its current model owner, signed immediate word, scaled truncation or stored word bits.");
            Check(machine.Variables.ReadByte(FieldBank.MapByte, 9) == 77, "D6 did not advance four bytes and continue in the same pass.");
            Check(JsonSerializer.Serialize(FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!, new Host()).Capture()) == JsonSerializer.Serialize(machine.Capture()), "Completed D6 cold restore changed VM state.");
        }
        BankedAndPendingOwner();
        ModelLessSkipsWithoutReadingOperand();
        Console.WriteLine("PASS native D6 word talk radius: signed scaling, word storage, bank/owner, model-less skip and cold continuation");
    }

    private static void BankedAndPendingOwner()
    {
        FieldProgram program = Program([0xA1, 0, 0, 0xD6, 0xE6, 31, 0xA5, 0x80, 0x50, 9, 77, 0x12, 0], 640);
        Host host = new(completeProperty: false);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.Variables.WriteWord(FieldBank.MapWord, 31, 1600);
        machine.AdvanceTick();
        FieldVmSnapshot saved = JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(machine.Capture()))!;
        FieldHostRequest request = saved.PendingRequests!.Single();
        Check(request.ProgramCounter == 107 && request.Command == new FieldActorProperty(FieldActorPropertyKind.TalkRadius, 2000), "D6 did not use the low bank selector and banked operand's byte address.");
        Host completing = new();
        FieldMachine cold = FieldMachine.Restore(program, saved, completing);
        cold.AdvanceTick();
        Check(completing.Requests.Single().Id == request.Id && cold.Variables.ReadByte(FieldBank.MapByte, 9) == 77, "D6 pending cold continuation lost its request identity or four-byte completion.");
    }

    private static void ModelLessSkipsWithoutReadingOperand()
    {
        FieldProgram program = Program([0, 0xD6, 8, 255, 255, 0x80, 0x50, 9, 77, 0x12, 0], 512);
        Host host = new();
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        machine.AdvanceTick();
        Check(host.Requests.Count == 0 && machine.Variables.ReadByte(FieldBank.MapByte, 9) == 77, "Model-less D6 must skip without reading an unsupported operand bank or publishing a host command.");
    }

    private static FieldProgram Program(byte[] code, short scale)
    {
        const int start = 104;
        byte[] section = new byte[start + code.Length + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(section, 0x0502);
        section[2] = 1;
        section[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(4), checked((ushort)(start + code.Length)));
        BinaryPrimitives.WriteInt16LittleEndian(section.AsSpan(8), scale);
        for (int script = 0; script < 32; script++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(section.AsSpan(40 + script * 2), start);
        }
        code.CopyTo(section, start);
        return FieldProgram.FromPcScripts(PcFieldScripts.Read(section));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Host(bool completeProperty = true) : IFieldHost
    {
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(request.Command is not FieldActorProperty || completeProperty);
        }
    }
}
