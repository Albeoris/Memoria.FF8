using System.Text.Json;
using Memoria.FF7.Events;

internal static class CycleCursorTests
{
    public static void Run()
    {
        FieldProgram program = Program();
        var host = new ThrowingHost();
        var machine = new FieldMachine(program, host: host);
        machine.Initialize();
        Reject<FieldExecutionException>(machine.AdvanceTick);
        Equal(new FieldCycleCursor(1, 6, 51), machine.CycleCursor);
        Equal(0L, machine.Tick);
        Equal((byte)1, machine.Variables.ReadByte(FieldBank.MapByte, 0));
        Equal((byte)6, machine.Variables.ReadByte(FieldBank.MapByte, 1));
        Equal((byte)0, machine.Variables.ReadByte(FieldBank.MapByte, 2));
        Equal((ushort)3, machine.Capture().Entities[0].WaitCounter);
        FieldVmSnapshot first = RoundTrip(machine.Capture());
        long requestId = first.PendingRequests!.Single().Id;
        Reject<FieldExecutionException>(machine.AdvanceTick);
        Equal(first, machine.Capture(), compareJson: true);
        Equal(requestId, host.Requests[1].Id);

        var coldHost = new ThrowingHost { Complete = true };
        FieldMachine restored = FieldMachine.Restore(program, first, coldHost);
        restored.AdvanceTick();
        Equal(requestId, coldHost.Requests.Single().Id);
        Equal(1L, restored.Tick);
        Equal<FieldCycleCursor?>(null, restored.CycleCursor);
        Equal((byte)1, restored.Variables.ReadByte(FieldBank.MapByte, 0));
        Equal((byte)7, restored.Variables.ReadByte(FieldBank.MapByte, 1));
        Equal((byte)1, restored.Variables.ReadByte(FieldBank.MapByte, 2));
        Equal((ushort)3, restored.Capture().Entities[0].WaitCounter);
        Equal(56, restored.Capture().Entities[1].ProgramCounter);
        Equal(0, restored.Capture().PendingRequests!.Length);
        var uninterrupted = new FieldMachine(program, host: new ThrowingHost { Complete = true });
        uninterrupted.Initialize();
        uninterrupted.AdvanceTick();
        Equal(uninterrupted.Capture(), restored.Capture(), compareJson: true);
        host.Complete = true;
        machine.AdvanceTick();
        Equal(restored.Capture(), machine.Capture(), compareJson: true);

        FieldVmSnapshot completed = restored.Capture();
        string legacyJson = JsonSerializer.Serialize(completed).Replace(",\"CycleCursor\":null", "", StringComparison.Ordinal);
        FieldMachine legacy = FieldMachine.Restore(program, JsonSerializer.Deserialize<FieldVmSnapshot>(legacyJson)!, coldHost);
        legacy.AdvanceTick();
        Equal(2L, legacy.Tick);
        Equal((ushort)2, legacy.Capture().Entities[0].WaitCounter);
        Equal((byte)8, legacy.Variables.ReadByte(FieldBank.MapByte, 1));

        foreach (FieldCycleCursor invalid in new[] { new FieldCycleCursor(-1, 0, 51), new FieldCycleCursor(3, 0, 51), new FieldCycleCursor(1, -1, 51), new FieldCycleCursor(1, 8, 51), new FieldCycleCursor(1, 6, 52) })
        {
            Reject<InvalidDataException>(() => FieldMachine.Restore(program, first with { CycleCursor = invalid }, coldHost));
        }
        Reject<InvalidDataException>(() => FieldMachine.Restore(program, first with { Initialized = false }, coldHost));
        FirstHandlerAndUnsupportedFault();
    }

    private static void FirstHandlerAndUnsupportedFault()
    {
        byte[] code = [0, 0x33, 1, 0x12, 0];
        var program = new FieldProgram(code, [Enumerable.Repeat((ushort)0, 32).ToArray()]);
        var host = new ThrowingHost();
        var machine = new FieldMachine(program, host: host);
        machine.Initialize();
        Reject<FieldExecutionException>(machine.AdvanceTick);
        Equal(new FieldCycleCursor(0, 0, 1), machine.CycleCursor);
        host.Complete = true;
        FieldMachine restored = FieldMachine.Restore(program, RoundTrip(machine.Capture()), host);
        restored.AdvanceTick();
        Equal(1L, restored.Tick);
        Equal<FieldCycleCursor?>(null, restored.CycleCursor);
        var unsupportedProgram = new FieldProgram([0, 0xFF], [Enumerable.Repeat((ushort)0, 32).ToArray()]);
        var unsupported = new FieldMachine(unsupportedProgram);
        unsupported.Initialize();
        Reject<FieldExecutionException>(unsupported.AdvanceTick);
        FieldMachine cold = FieldMachine.Restore(unsupportedProgram, RoundTrip(unsupported.Capture()));
        Equal(new FieldCycleCursor(0, 0, 1), cold.CycleCursor);
        Reject<FieldExecutionException>(cold.AdvanceTick);
        Equal(0L, cold.Tick);
    }

    private static FieldProgram Program()
    {
        byte[] code = new byte[128];
        new byte[] { 0, 0x95, 5, 0, 0x24, 3, 0, 0x12, 0 }.CopyTo(code, 0);
        code[32] = 0;
        for (int index = 0; index < 6; index++)
        {
            new byte[] { 0x95, 5, 1 }.CopyTo(code, 33 + index * 3);
        }
        new byte[] { 0x33, 1, 0x95, 5, 1, 0x95, 5, 1, 0x12, 0 }.CopyTo(code, 51);
        new byte[] { 0, 0x95, 5, 2, 0x24, 0, 0, 0x12, 0 }.CopyTo(code, 96);
        return new FieldProgram(code, new[] { 0, 32, 96 }.Select(offset => Enumerable.Repeat((ushort)offset, 32).ToArray()).ToArray());
    }

    private static FieldVmSnapshot RoundTrip(FieldVmSnapshot snapshot) => JsonSerializer.Deserialize<FieldVmSnapshot>(JsonSerializer.Serialize(snapshot))!;

    private static void Equal<T>(T expected, T actual, bool compareJson = false)
    {
        if (compareJson ? JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(actual) : !EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Cycle cursor expected {JsonSerializer.Serialize(expected)}, got {JsonSerializer.Serialize(actual)}.");
        }
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception($"Expected {typeof(T).Name}.");
    }

    private sealed class ThrowingHost : IFieldHost
    {
        public bool Complete { get; set; }
        public List<FieldHostRequest> Requests { get; } = [];

        public FieldHostReply Execute(FieldHostRequest request)
        {
            Requests.Add(request);
            if (!Complete)
            {
                throw new NotSupportedException("Required test host operation is unavailable.");
            }
            return new(true);
        }
    }
}
