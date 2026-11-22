using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePaletteRotateTests
{
    private sealed class RecordingHost : IFieldHost
    {
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return new(true);
        }
    }

    internal static void Run()
    {
        CheckVector(0, 16, 1, [3, 4, 1, 2, 3]);
        CheckVector(0, 16, 0, [4, 1, 2, 3, 4]);
        CheckVector(0, 0, 1, [0, 0, 0, 0, 0]);
        CheckVector(1, 0, 2, [18, 19, 20, 17, 18]);
        CheckVector(1, 0, 5, [15, 16, 17, 18, 19, 20]);
        Ff7FieldHost maximum = Host();
        maximum.Execute(new(1, 0, 1, 8, new FieldPaletteCommand(FieldPaletteOperation.Rotate, 0, 32, 256, Rotation: 0)));
        Check(maximum.Process.PaletteBuffer[512] == 256 && maximum.Process.PaletteBuffer[768] == 256 && maximum.Process.PaletteBuffer[769] == 769, "Native rotate count256 did not perform its inclusive257-word operation.");
        foreach (FieldPaletteCommand invalid in new[]
        {
            new FieldPaletteCommand(FieldPaletteOperation.Rotate, 63, 0, 256, Rotation: 0),
            new FieldPaletteCommand(FieldPaletteOperation.Rotate, 0, 63, 256, Rotation: 0),
            new FieldPaletteCommand(FieldPaletteOperation.Rotate, 0, 0, 4, Rotation: 5)
        })
        {
            Ff7FieldHost host = Host();
            string before = JsonSerializer.Serialize(host.Process);
            bool rejected = false;
            try
            {
                host.Execute(new(1, 0, 1, 8, invalid));
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(rejected && before == JsonSerializer.Serialize(host.Process), "An out-of-buffer rotate was accepted or partially published.");
        }
        CheckDecode(false);
        CheckDecode(true);
        FieldPaletteCommand old = JsonSerializer.Deserialize<FieldPaletteCommand>("{\"Operation\":2,\"Source\":0,\"Destination\":16,\"Count\":4}")!;
        Check(old.Rotation == 0, "An old palette command acquired an invented rotation.");
        Console.WriteLine("Native palette rotation preserves inclusive sequential overlap, operand decoding and atomic bounds.");
    }

    private static void CheckVector(int source, int destination, int rotation, ushort[] expected)
    {
        Ff7FieldHost host = Host();
        host.Execute(new(1, 0, 1, 8, new FieldPaletteCommand(FieldPaletteOperation.Rotate, source, destination, 4, Rotation: rotation)));
        Check(host.Process.PaletteBuffer.AsSpan(destination * 16, expected.Length).SequenceEqual(expected), "The native sequential rotation vector differs, including aliasing or negative relative source indexing.");
    }

    private static void CheckDecode(bool mapped)
    {
        byte[] code = new byte[20];
        new byte[] { 0xE8, mapped ? (byte)0x55 : (byte)0, mapped ? (byte)0x05 : (byte)0, mapped ? (byte)10 : (byte)1, mapped ? (byte)11 : (byte)16, mapped ? (byte)12 : (byte)2, 3, 0 }.CopyTo(code, 8);
        ushort[][] entries = [new ushort[32]];
        entries[0][1] = 8;
        RecordingHost host = new();
        FieldMachine machine = new(new FieldProgram(code, entries), host: host);
        machine.Initialize();
        machine.Variables.WriteByte(FieldBank.MapByte, 10, 1);
        machine.Variables.WriteByte(FieldBank.MapByte, 11, 16);
        machine.Variables.WriteByte(FieldBank.MapByte, 12, 2);
        Check(machine.RequestExternalScript(0, 1).Started, "The rotate decoder fixture did not start its native entry.");
        machine.AdvanceTick();
        Check(host.Requests.Count == 1 && host.Requests[0].ProgramCounter == 8 && host.Requests[0].Command == new FieldPaletteCommand(FieldPaletteOperation.Rotate, 1, 16, 4, Rotation: 2), "E8 bank selectors or seven-byte rotate operands decoded incorrectly.");
        Check((machine.Capture().PendingRequests ?? []).Length == 0 && machine.Capture().Entities[0].Priority == 7, "Native rotate yielded or failed to execute its following RET in the same callback.");
    }

    private static Ff7FieldHost Host()
    {
        Ff7FieldResources field = new("palette-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, new() { PaletteBuffer = Enumerable.Range(0, 1024).Select(value => (ushort)value).ToArray() });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
