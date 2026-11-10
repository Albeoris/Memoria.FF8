using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class WorldMachineTests
{
    public static void Run()
    {
        AliasesAndArithmetic();
        SchedulerAndColdRestore();
        CorruptionAndUnsupported();
        Console.WriteLine("PASS world VM bank aliases, signed operands, native scheduler, cold snapshot and corruption");
    }

    private static void AliasesAndArithmetic()
    {
        ushort[] code = [0, 0x11B, 0, 0x110, 255, 0xE0, 0x100, 0x11A, 0, 0x303, 0x11C, 0, 0x110, 1, 0x41, 0x303, 0x114, 32, 0x110, 1, 0xE0, 0x100, 0x203];
        byte[] persistent = new byte[1280];
        persistent[0] = 254;
        persistent[1] = 255;
        Host host = new();
        WorldMachine machine = new(new(new([new(0, 1)], code)), persistent, host);
        machine.StartSystem(0);
        machine.Tick();
        Check(host.Requests.Count == 2 && host.Requests[0].Command.Arguments[0] == 255 && host.Requests[1].Command.Arguments[0] == -3, "Local banks must alias and signed short arithmetic must retain native order.");
        Check(persistent[4] == 1 && machine.CopyPersistent()[4] == 1, "World bank zero must alias the caller's persistent variable storage.");
    }

    private static void SchedulerAndColdRestore()
    {
        ushort[] code = [0, 0x110, 3, 0x300, 0x203, 0x110, 2, 0x305, 0x306, 0x203, 0x110, 17, 0x303, 0x203];
        PcWorldEvents events = new([new(0, 1), new(0x4300, 5), new(0x4302, 10)], code);
        Host originalHost = new();
        WorldMachine original = new(new(events), new byte[1280], originalHost);
        original.StartSystem(0);
        original.Tick();
        Check(originalHost.Requests.Count == 2 && originalHost.Requests[0].ExecutingModel == -1 && originalHost.Requests[0].ActiveModel == 3 && originalHost.Requests[1].ExecutingModel == 3, "Allocated models must run after the system in native prepend order.");
        WorldSnapshot snapshot = original.Capture();
        Host restoredHost = new();
        WorldMachine restored = new(new(events), original.CopyPersistent(), restoredHost);
        restored.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Check(JsonSerializer.Serialize(snapshot) == JsonSerializer.Serialize(restored.Capture()), "Cold restore must preserve PCs, stack, counters and request sequence without execution.");
        originalHost.Requests.Clear();
        for (int i = 0; i < 3; i++)
        {
            original.Tick();
            restored.Tick();
        }
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()) && JsonSerializer.Serialize(originalHost.Requests) == JsonSerializer.Serialize(restoredHost.Requests), "Cold continuation must execute identical native scheduler operations.");
        Check(restoredHost.Requests.Count(value => value.Command.Operation == WorldOperation.Move) == 1 && restoredHost.Requests.Any(value => value.Command.Operation == WorldOperation.Speed && value.Command.Arguments[0] == 17), "Move counters must yield once per native tick before on_tick starts.");
    }

    private static void CorruptionAndUnsupported()
    {
        WorldMachine machine = new(new(new([new(0, 1)], new ushort[] { 0, 0x203 })), new byte[1280], new Host());
        WorldSnapshot snapshot = machine.Capture();
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { Local = new byte[63] }));
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { Stack = new WorldStackValue[5] }));
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { System = snapshot.System with { ProgramCounter = 2 } }));
        WorldMachine unsupported = new(new(new([new(0, 1)], new ushort[] { 0, 0x356 })), new byte[1280], new Host());
        unsupported.StartSystem(0);
        Throws<NotSupportedException>(unsupported.Tick);
        WorldMachine underflow = new(new(new([new(0, 1)], new ushort[] { 0, 0x303 })), new byte[1280], new Host());
        underflow.StartSystem(0);
        Throws<InvalidDataException>(underflow.Tick);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class Host : IWorldHost
    {
        public List<WorldHostRequest> Requests { get; } = [];

        public WorldHostReply Execute(WorldHostRequest request)
        {
            Requests.Add(request);
            return new(true);
        }

        public int ReadSpecial(int selector, int activeModel)
        {
            throw new NotSupportedException($"Unexpected special selector {selector}.");
        }
    }
}
