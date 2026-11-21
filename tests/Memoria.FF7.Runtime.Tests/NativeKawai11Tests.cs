using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeKawai11Tests
{
    internal static void Run()
    {
        Ff7Actor legacy = JsonSerializer.Deserialize<Ff7Actor>("{\"Slot\":0,\"Entity\":0,\"Model\":0}")!;
        Check(legacy.NativeModelCompletion is null, "An old actor save invented pending selector11 presentation work.");
        Ff7NativeModelCompletionState state = new(11, [7, 8, 9]);
        state.Validate();
        Check(JsonSerializer.Deserialize<Ff7NativeModelCompletionState>(JsonSerializer.Serialize(state))!.Payload.SequenceEqual(state.Payload), "A cold selector11 owner changed its opaque native payload.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 4096), 0, 256);
        Ff7FieldResources field = new("kawai11-fixture", null!, null!, null!, [camera], [], null!, null!, null!);
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, Visible = false };
        Ff7Actor far = actor with { Slot = 1, Entity = 1, Z4096 = 11274 * 4096 };
        Check(Ff7FieldHost.ModelEffectDepth(far, camera) == 3840 && Ff7FieldHost.ModelEffectDepth(far with { Z4096 = 11273 * 4096 }, camera) == 3839, "The native strict-depth boundary changed.");
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [actor, far] });
        string pristine = JsonSerializer.Serialize(host.Capture());
        bool rejected = false;
        try
        {
            host.Execute(new(99, 0, 6, 2, new FieldModelEffectCommand(11, 8) { Payload = [7] }));
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected && pristine == JsonSerializer.Serialize(host.Capture()), "A mismatched decoded mode/payload was admitted or changed native state.");
        byte[] code = [0, 0, 0x28, 6, 11, 7, 8, 9, 0x29, 0x5F, 0];
        byte[] original = code.ToArray();
        FieldProgram program = new(code, [new ushort[32]]);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        FieldVmSnapshot initialized = machine.Capture();
        machine = FieldMachine.Restore(program, initialized with { Entities = [initialized.Entities[0] with { ProgramCounter = 2, ModelBound = true }] }, host);
        machine.AdvanceTick();
        Check(machine.Capture().Entities[0].ProgramCounter == 8 && host.Process.Actors[0].NativeModelCompletion is { Operation: 11, Pending: true, Payload: [7, 8, 9] }, "Selector11 did not acknowledge its declared packet length and leave the separate KAWIW pending.");
        Check(host.Execute(new(100, 1, 6, 2, new FieldModelEffectCommand(11, 255) { Payload = [255, 1] })).Completed, "A second physical selector11 owner rejected its unused raw payload.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed the eligible selector11 callback.");
        Check(host.Process.Actors[0].NativeModelCompletion is { Pending: false, Payload: [7, 8, 9] } && host.Process.Actors[1].NativeModelCompletion is { Pending: true }, "Completion ignored the strict depth gate or required the otherwise hidden model to become visible.");
        Check(host.Process.Actors[0].Visible == false && host.Process.Actors[0].NativeColorOffset is null && host.Process.Actors[0].PartVisibility is null, "Selector11 invented a visual color or part-visibility effect.");
        machine.AdvanceTick();
        Check(machine.Capture().Entities[0].ProgramCounter == 10, "KAWIW did not consume the real eligible callback completion.");
        Check(code.SequenceEqual(original), "Selector11 processing changed original script bytes.");
        FieldHostRequest waitFar = new(101, 1, 6, 8, new FieldModelEffectCommand(13, Wait: true));
        Check(!host.Execute(waitFar).Completed, "An out-of-depth owner completed KAWIW without an eligible callback.");
        Check(host.Execute(new(102, 1, 6, 2, new FieldModelEffectCommand(12, null) { Payload = [] })).Completed && host.Process.Actors[1].NativeModelCompletion is { Operation: 12, Pending: true }, "An accepted replacement did not replace only its own physical pending selector.");
        Check(host.Process.Actors[0].NativeModelCompletion is { Operation: 11, Pending: false }, "Replacing another actor's owner changed the completed selector11 state.");
        Console.WriteLine("Native KAWAI11 preserves packet bytes and physical pending ownership through strict-depth callbacks and KAWIW.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
