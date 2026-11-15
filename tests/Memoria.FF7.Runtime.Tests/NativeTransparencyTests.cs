using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTransparencyTests
{
    public static void Run()
    {
        byte[] payload = [173, 42];
        Ff7NativeTransparencyState pending = Ff7NativeTransparency.Request(null, payload);
        payload[0] = 0;
        Check(pending is { Mode: 0, Pending: true } && pending.Request!.SequenceEqual(new byte[] { 173, 42 }), "Transparency must copy its complete declared payload without applying the raw mode at dispatch.");
        Check(ReferenceEquals(pending, Ff7NativeTransparency.Advance(pending, false)), "Ineligible processing consumed the transparency request.");
        Ff7NativeTransparencyState applied = Ff7NativeTransparency.Advance(pending, true);
        Check(applied is { Mode: 173, Pending: false }, "Eligible processing must preserve the raw nonzero mode and retire its request.");
        Check(ReferenceEquals(applied, Ff7NativeTransparency.Advance(applied, true)), "A retired transparency request advanced again.");
        Ff7NativeTransparencyState replaced = Ff7NativeTransparency.Request(Ff7NativeTransparency.Request(applied, [1]), [0, 99]);
        Check(replaced.Mode == 173 && replaced.Request!.SequenceEqual(new byte[] { 0, 99 }), "A newer pending request must replace the payload while retaining the displayed mode.");
        Check(Ff7NativeTransparency.Advance(replaced, true) is { Mode: 0, Pending: false }, "A replacement zero request did not clear transparency on its eligible callback.");
        Ff7NativeTransparencyState cold = JsonSerializer.Deserialize<Ff7NativeTransparencyState>(JsonSerializer.Serialize(replaced))!;
        Check(JsonSerializer.Serialize(Ff7NativeTransparency.Advance(cold, true)) == JsonSerializer.Serialize(Ff7NativeTransparency.Advance(replaced, true)), "Cold pending transparency changed its next callback.");
        Ff7Actor legacy = JsonSerializer.Deserialize<Ff7Actor>("{\"Slot\":0,\"Entity\":0,\"Model\":0}")!;
        Check(legacy.NativeTransparency is null && Ff7NativeTransparency.Request(legacy.NativeTransparency, [1]).Mode == 0, "An older actor must restore the native opaque default without replay.");
        Throws<InvalidDataException>(() => Ff7NativeTransparency.Request(null, []));
        RunInstalled();
        Console.WriteLine("PASS native KAWAI1 deferred mode, replacement, eligibility, wait, cancellation and cold state");
    }

    private static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original transparency resource: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "gldst");
        byte[] instruction = field.Scripts.Data.Span.Slice(0x13E8, 4).ToArray();
        Check(instruction.SequenceEqual(new byte[] { 0x28, 4, 1, 1 }), "The original reached transparency instruction or its declared length changed.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 4096), 0, 256);
        field = field with { Cameras = [camera] };
        Ff7Actor hidden = new() { Slot = 0, Entity = 0, Model = 0, Visible = false };
        Ff7Actor distant = new() { Slot = 1, Entity = 1, Model = 0, Z4096 = 20000 * 4096 };
        Check(Ff7FieldHost.ModelEffectDepth(hidden, camera) < 3840 && Ff7FieldHost.ModelEffectDepth(distant, camera) >= 3840, "The eligibility fixture does not straddle the native projected-depth gate.");
        Check(Ff7FieldHost.ModelEffectDepth(hidden with { Z4096 = 11273 * 4096 }, camera) == 3839 && Ff7FieldHost.ModelEffectDepth(hidden with { Z4096 = 11274 * 4096 }, camera) == 3840, "The native depth boundary must remain strict at 3840.");
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [hidden, distant] });
        byte[] code = new byte[0x13F0];
        instruction.CopyTo(code, 0x13E8);
        code[0x13EC] = 0x29;
        FieldProgram program = new(code, [new ushort[32]]);
        FieldMachine vm = new(program, host: host);
        vm.Initialize();
        FieldVmSnapshot initialized = vm.Capture();
        vm = FieldMachine.Restore(program, initialized with { Entities = [initialized.Entities[0] with { ProgramCounter = 0x13E8, ModelBound = true }] }, host);
        vm.AdvanceTick();
        Check(vm.Capture().Entities[0].ProgramCounter == 0x13EC && host.Process.Actors[0].NativeTransparency is { Mode: 0, Pending: true }, "KAWAI1 must immediately advance by the original declared length before KAWIW waits at opcode29.");
        Check(host.Capture().Receipts.Any(receipt => receipt.Request.ProgramCounter == 0x13E8 && receipt.Request.Command is FieldModelEffectCommand { Operation: 1, Mode: 1 } && receipt.Reply.Completed), "The original instruction did not complete its host request at the source PC.");
        Check(host.Execute(new(100, 1, 7, 0x13E8, new FieldModelEffectCommand(1, 7) { Payload = [7] })).Completed, "A second physical owner did not queue independently.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold transparency changed the next model callback.");
        Check(host.Process.Actors[0].NativeTransparency is { Mode: 1, Pending: false } && host.Process.Actors[1].NativeTransparency is { Mode: 0, Pending: true }, "Model processing must ignore visibility while retaining the projected-depth3840 gate.");
        vm.AdvanceTick();
        Check(vm.Capture().Entities[0].ProgramCounter != 0x13EC, "KAWIW did not retire after the eligible model pass.");
        Check(host.Execute(new(101, 0, 7, 0x13E8, new FieldModelEffectCommand(1, 219) { Payload = [219] })).Completed, "The raw nonzero mode request was rejected.");
        host.AdvanceActors();
        Check(host.Process.Actors[0].NativeTransparency is { Mode: 219, Pending: false }, "The host normalized a raw transparency mode.");
        host.Execute(new(102, 0, 7, 0x13E8, new FieldModelEffectCommand(1) { Payload = [0] }));
        host.Execute(new(103, 0, 7, 0x13E8, new FieldModelEffectCommand(2) { Payload = new byte[6] }));
        Check(host.Process.Actors[0].NativeTransparency is { Mode: 219, Pending: false } && host.Process.Actors[1].NativeTransparency is { Pending: true }, "A different selector must cancel only its owner's pending request while retaining its applied raw mode.");
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
