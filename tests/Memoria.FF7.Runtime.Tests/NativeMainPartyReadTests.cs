using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativeMainPartyReadTests
{
    private sealed class RecordingHost(int value) : IFieldHost
    {
        internal List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            return request.Command is FieldPartyCommand { Operation: FieldPartyOperation.ReadSlot } ? new(true, value) : throw new InvalidOperationException("Unexpected native host operation.");
        }
    }

    internal static void Run()
    {
        foreach (int value in new[] { 0, 255, 7 })
        {
            foreach (byte slot in new byte[] { 0, 1, 2 })
            {
                RecordingHost host = new(value);
                var (program, machine) = Machine(0xF5, slot, 17, host);
                machine.Variables.WriteByte(FieldBank.Persistent4Byte, slot, (byte)((slot + 1) % 3));
                FieldVmSnapshot source = machine.Capture();
                string before = JsonSerializer.Serialize(source);
                RecordingHost restoredHost = new(value);
                FieldMachine restored = FieldMachine.Restore(program, source, restoredHost);
                restored.AdvanceTick();
                Check(restored.Variables.ReadByte(FieldBank.MapByte, 17) == value, "Main-party slot did not preserve its unsigned native byte result.");
                Check(restored.Variables.ReadByte(FieldBank.MapByte, 126) == 77, "A completed main-party read yielded before its next native instruction.");
                Check(restoredHost.Requests is [{ Entity: 0, Priority: 7, ProgramCounter: 1, Command: FieldPartyCommand { Operation: FieldPartyOperation.ReadSlot } command }] && command.Member0 == slot, "The literal slot or native request owner/PC was changed by a packed bank selector.");
                Check((restored.Capture().PendingRequests ?? []).Length == 0, "A completed native read retained pending host debt.");
                Check(before == JsonSerializer.Serialize(source) && before == JsonSerializer.Serialize(machine.Capture()), "Cold native read continuation altered its source snapshot or original machine.");
            }
        }
        foreach (byte slot in new byte[] { 3, 255 })
        {
            RecordingHost host = new(7);
            var (_, machine) = Machine(0xF5, slot, 17, host);
            machine.Variables.WriteByte(FieldBank.MapByte, 17, 91);
            machine.AdvanceTick();
            Check(host.Requests.Count == 0 && machine.Variables.ReadByte(FieldBank.MapByte, 17) == 91 && machine.Variables.ReadByte(FieldBank.MapByte, 126) == 77, "An invalid literal slot issued a host read, wrote a result or failed to advance four bytes.");
        }
        RecordingHost persistentHost = new(255);
        var (_, persistent) = Machine(0x61, 2, 18, persistentHost);
        persistent.AdvanceTick();
        Check(persistent.Variables.ReadByte(FieldBank.Persistent0Byte, 18) == 255 && persistent.Variables.ReadByte(FieldBank.MapByte, 18) == 0, "Opcode74 did not use only the low packed nibble for its destination bank.");
        CheckMetadata();
        Console.WriteLine("Native main-party literal slots, unsigned results, packed destination banks, continuation and source purity passed.");
    }

    private static void CheckMetadata()
    {
        RecordingHost host = new(0);
        var (_, machine) = Machine(0x05, 1, 17, host);
        string before = JsonSerializer.Serialize(machine.Capture());
        FieldScriptAnalysis analysis = machine.AnalyzeScriptAtProgramCounter(0, 1);
        Check(analysis.Complete && analysis.HostReads.Any(read => read.Kind == FieldHostReadKind.MainPartySlot && read.Parameter == 1), "Main-party reads were conflated with script-party projection metadata.");
        Check(before == JsonSerializer.Serialize(machine.Capture()) && host.Requests.Count == 0, "Main-party dependency inspection called the live host or changed native state.");
    }

    private static (FieldProgram Program, FieldMachine Machine) Machine(byte packed, byte slot, byte destination, IFieldHost host)
    {
        byte[] code = [0, 0x74, packed, slot, destination, 0x80, 0x50, 126, 77, 0x24, 1, 0, 0];
        FieldProgram program = new(code, [new ushort[32]]);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        return (program, machine);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
