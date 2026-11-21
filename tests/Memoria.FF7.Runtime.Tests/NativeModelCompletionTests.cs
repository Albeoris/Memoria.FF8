using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeModelCompletionTests
{
    public static void Run()
    {
        Ff7Actor legacy = JsonSerializer.Deserialize<Ff7Actor>("{\"Slot\":0,\"Entity\":0,\"Model\":0}")!;
        Check(legacy.NativeModelCompletion is null && !JsonSerializer.Serialize(legacy).Contains("NativeModelCompletion", StringComparison.Ordinal), "An absent presentation owner changed old actor compatibility or serialization.");
        Throws<InvalidDataException>(() => new Ff7NativeModelCompletionState(14, [0]).Validate());
        new Ff7NativeModelCompletionState(12, []).Validate();
        new Ff7NativeModelCompletionState(255, []).Validate();
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original model completion resource: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "jtmpin1");
        byte[] instruction = field.Scripts.Data.Span.Slice(0x077F, 13).ToArray();
        Check(instruction.SequenceEqual(new byte[] { 0x28, 13, 12, 0, 0, 0, 0, 0, 0, 0, 16, 30, 0 }), "The reached original completion packet differs from its source.");
        Check(field.Scripts.Data.Span.Slice(0x078F, 4).SequenceEqual(new byte[] { 0x28, 4, 12, 1 }), "The following original one-byte completion payload differs from its source.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 4096), 0, 256);
        field = field with { Cameras = [camera] };
        Ff7Actor hidden = new() { Slot = 0, Entity = 0, Model = 0, Visible = false };
        Ff7Actor distant = new() { Slot = 1, Entity = 1, Model = 0, Z4096 = 11274 * 4096 };
        Check(Ff7FieldHost.ModelEffectDepth(hidden with { Z4096 = 11273 * 4096 }, camera) == 3839 && Ff7FieldHost.ModelEffectDepth(distant, camera) == 3840, "Completion eligibility must use the strict native depth boundary.");
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [hidden, distant] });
        byte[] code = new byte[0x0790];
        instruction.CopyTo(code, 0x077F);
        code[0x078C] = 0x29;
        code[0x078D] = 0x5F;
        FieldProgram program = new(code, [new ushort[32]]);
        FieldMachine vm = new(program, host: host);
        vm.Initialize();
        FieldVmSnapshot initialized = vm.Capture();
        vm = FieldMachine.Restore(program, initialized with { Entities = [initialized.Entities[0] with { ProgramCounter = 0x077F, ModelBound = true }] }, host);
        vm.AdvanceTick();
        Check(vm.Capture().Entities[0].ProgramCounter == 0x078C && host.Process.Actors[0].NativeModelCompletion is { Operation: 12, Pending: true }, "The request must immediately acknowledge its declared length while KAWIW waits for the separate model callback.");
        Check(host.Execute(new(100, 1, 7, 0x078F, new FieldModelEffectCommand(12, 255) { Payload = [255, 7] })).Completed, "A second physical owner or unused raw payload was rejected.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold completion changed the next model callback.");
        Check(host.Process.Actors[0].NativeModelCompletion is { Pending: false } && host.Process.Actors[1].NativeModelCompletion is { Pending: true }, "Visibility or another physical slot changed native callback eligibility.");
        vm.AdvanceTick();
        Check(vm.Capture().Entities[0].ProgramCounter == 0x078E, "KAWIW did not retire immediately after eligible completion.");
        Check(host.Execute(new(101, 0, 7, 0x078F, new FieldModelEffectCommand(12, 1) { Payload = [1] })).Completed, "The one-byte replacement request failed.");
        Check(host.Process.Actors[0].NativeModelCompletion is { Pending: true, Payload: [1] }, "A replacement request did not reestablish its pending owner.");
        host.Execute(new(102, 0, 7, 0x078F, new FieldModelEffectCommand(2) { Payload = new byte[6] }));
        Check(host.Process.Actors[0].NativeModelCompletion is null && host.Process.Actors[1].NativeModelCompletion is { Pending: true }, "Another accepted selector failed to replace only the current physical pending owner.");
        RunPayloadFree(field, camera, hidden, distant);
        Console.WriteLine("PASS native selector12 packet, physical pending owner, strict depth, KAWIW, replacement and old/cold presentation state");
    }

    private static void RunPayloadFree(Ff7FieldResources field, PcFieldCamera camera, Ff7Actor hidden, Ff7Actor distant)
    {
        Ff7FieldResources original = Ff7FieldResources.Load(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), "kuro_1");
        Check(original.Scripts.Data.Span.Slice(0x0EA3, 4).SequenceEqual(new byte[] { 0x28, 3, 255, 0 }), "The original payload-free request consumed a following script byte.");
        Ff7NativeColorOffsetState color = new() { Red = 3, Green = -4, Blue = 5 };
        hidden = hidden with { PartVisibility = new() { Visibility = [0, 1] }, NativeColorOffset = color };
        distant = distant with { PartVisibility = new() { Visibility = [1, 0] } };
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [hidden, distant] }, fieldId: 604);
        Check(host.Execute(new(200, 0, 6, 0x0EA3, new FieldModelEffectCommand(255, null) { Payload = [] })).Completed, "Payload-free FF did not acknowledge its native producer immediately.");
        Check(host.Execute(new(201, 1, 6, 0x0EA3, new FieldModelEffectCommand(255, null) { Payload = [] })).Completed, "The second physical FF owner was rejected.");
        FieldHostRequest wait = new(202, 0, 6, 0x0EA6, new FieldModelEffectCommand(13, Wait: true));
        Check(!host.Execute(wait).Completed && host.Process.Actors[0].PartVisibility!.Visibility.SequenceEqual(new byte[] { 0, 1 }), "FF applied visibility at dispatch or completed its wait before the model callback.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!, fieldId: 604);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Payload-free FF changed its cold next callback.");
        Check(host.Process.Actors[0].PartVisibility!.Visibility.SequenceEqual(new byte[] { 1, 1 }) && host.Process.Actors[1].PartVisibility!.Visibility.SequenceEqual(new byte[] { 1, 0 }), "FF failed to enable every eligible original part or crossed the strict depth gate.");
        Check(host.Process.Actors[0].NativeColorOffset == color && host.Process.Actors[0].NativeModelCompletion is { Operation: 255, Payload: [], Pending: false } && host.Execute(wait).Completed, "FF invented color restoration, payload or an extra completion callback.");
        Check(host.Execute(new(203, 0, 6, 0x0EA3, new FieldModelEffectCommand(12, null) { Payload = [] })).Completed, "The proved selector12 callback incorrectly requires an unread payload byte.");
        host.AdvanceActors();
        Check(host.Process.Actors[0].NativeModelCompletion is { Operation: 12, Payload: [], Pending: false }, "Empty selector12 failed its native one-callback completion.");
        Ff7NativeModelEffectState pending = Ff7NativeModelEffect.Request(null, 0, [35]);
        Ff7FieldHost unprocessed = new(field, (_, _) => 1, new() { Actors = [hidden with { ModelEffect = pending }] }, fieldId: 604);
        Check(unprocessed.Execute(new(204, 0, 6, 0x0EA3, new FieldModelEffectCommand(255, null) { Payload = [] })).Completed && unprocessed.Process.Actors[0].ModelEffect is null, "An unprocessed request was mistaken for initialized native procedural ownership.");
        Ff7FieldHost initialized = new(field, (_, _) => 1, new() { Actors = [hidden with { PartVisibility = null, ModelEffect = Ff7NativeModelEffect.Advance(pending, true) }] }, fieldId: 604);
        string before = JsonSerializer.Serialize(initialized.Capture());
        Throws<NotSupportedException>(() => initialized.Execute(new(205, 0, 6, 0x0EA3, new FieldModelEffectCommand(255, null) { Payload = [] })));
        Check(before == JsonSerializer.Serialize(initialized.Capture()), "Unsupported native13 prelude replacement cleared applied presentation.");
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
}
