using System.Text.Json;
using Memoria.FF7.Events;

internal static class NativePartySpatialPredicateTests
{
    private sealed class RejectHost : IFieldHost
    {
        internal int Calls { get; private set; }
        public FieldHostReply Execute(FieldHostRequest request)
        {
            Calls++;
            throw new InvalidOperationException("Spatial inspection must not execute live host commands.");
        }
    }

    internal static void Run()
    {
        byte[] body = [0xCB, 0, 16, 0xB9, 6, 1, 7, 0x16, 0x60, 7, 0, 14, 0, 0, 4, 0x03, 1, 0x44, 0];
        var fixture = Create(body);
        FieldSpatialSample[] samples = [new(14, new(0, 0, 0, 14)), new(15, new(0, 0, 0, 15))];
        string source = JsonSerializer.Serialize(fixture.Machine.Capture());
        byte[] original = body.ToArray();
        FieldSpatialObservation observed = fixture.Machine.ReadCurrentSpatialDemands(1, samples);
        Check(observed.Demands.Single() is { SampleId: 14, TriangleOnly: true, TargetEntity: 1, TargetScript: 4 } demand && demand.DestinationFields.SequenceEqual([100]), "Pure current party membership hid the actual controlled-triangle transition.");
        Check(observed.Reads.Any(read => read.Bank == FieldBank.Persistent1Byte && read.Operand == 9), "Party membership lost its actual persistent party refresh dependency.");
        Check(source == JsonSerializer.Serialize(fixture.Machine.Capture()) && fixture.Host.Calls == 0 && body.SequenceEqual(original), "Spatial party inspection changed the native VM, source bytes or host.");
        FieldMachine cold = FieldMachine.Restore(fixture.Program, JsonSerializer.Deserialize<FieldVmSnapshot>(source)!, fixture.Host);
        Check(JsonSerializer.Serialize(observed) == JsonSerializer.Serialize(cold.ReadCurrentSpatialDemands(1, samples)), "Cold restoration changed native party-dependent spatial advice.");
        fixture.Machine.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 1);
        FieldSpatialObservation absent = fixture.Machine.ReadCurrentSpatialDemands(1, samples);
        Check(absent.Demands.Length == 0 && absent.Fingerprint != observed.Fingerprint, "Absent party membership retained the old transition or source fingerprint.");
        fixture.Machine.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 0);
        FieldSpatialObservation reordered = fixture.Machine.ReadCurrentSpatialDemands(1, samples);
        Check(reordered.Demands.Single().SampleId == 14 && reordered.Fingerprint != observed.Fingerprint, "A changed actual party layout failed to refresh the same feasible native triangle branch.");
        Check(fixture.Machine.ReadCurrentSpatialDemands(2, samples).Demands.Length == 0, "A query for another actor was substituted with the controlled actor's samples.");
        foreach (byte[] prefix in new byte[][] { [0xCC, 0, 1], [0xCD, 1, 0] })
        {
            var unsupported = Create([.. prefix, .. body]);
            string before = JsonSerializer.Serialize(unsupported.Machine.Capture());
            Check(unsupported.Machine.ReadCurrentSpatialDemands(1, samples).Demands.Length == 0 && unsupported.Host.Calls == 0 && before == JsonSerializer.Serialize(unsupported.Machine.Capture()), "Unsupported host party availability or mutation was promoted to pure membership inspection.");
        }
        Console.WriteLine("Native pure party membership retains current controlled-triangle predicates and party-dependent source fingerprints.");
    }

    private static (FieldProgram Program, FieldMachine Machine, RejectHost Host) Create(byte[] body)
    {
        ushort[] entries = new ushort[32];
        entries[4] = checked((ushort)(body.Length + 2));
        byte[] transition = [0x60, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        FieldProgram program = new([0, .. body, 0, .. transition], [new ushort[32], entries, new ushort[32]]);
        RejectHost host = new();
        FieldMachine initialized = new(program, host: host);
        initialized.Initialize();
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 9, 0);
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 10, 255);
        initialized.Variables.WriteByte(FieldBank.Persistent1Byte, 11, 255);
        FieldVmSnapshot vm = initialized.Capture();
        vm.Entities[1] = vm.Entities[1] with { ModelBound = true, PartyCharacter = 0, ProgramCounter = body.Length + 1 };
        vm.Entities[2] = vm.Entities[2] with { ModelBound = true, PartyCharacter = 1, ProgramCounter = body.Length + 1 };
        return (program, FieldMachine.Restore(program, vm, host), host);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
