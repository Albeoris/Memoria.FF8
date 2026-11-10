using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeFixedEyeControlTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP fixed native eye control: original field resources are not configured.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "ealin_2");
        byte[] authored = field.Scripts.Data.Span.Slice(0x0769, 8).ToArray();
        Check(authored.AsSpan().SequenceEqual(new byte[] { 0x28, 7, 0, 2, 2, 0, 0, 0x29 }), "The installed fixed-eye fixture no longer matches its original instruction and following KAWIW.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 15356), 0, 1000);
        Ff7FieldResources eligible = field with { Cameras = [camera] };
        Ff7FieldResources rejected = field with { Cameras = [camera with { Translation = new(0, 0, 15360) }] };
        Ff7Actor actor = new() { Slot = 0, Entity = 1, Model = 0, Visible = false, Z4096 = 10 * 4096, BlinkCountdown = 23, NativeEyeLeft = 1, NativeEyeRight = 1 };
        Ff7FieldProcess initial = new() { Actors = [actor], BlinkStateInitialized = true, BlinkRandomIndex = 77 };
        Ff7FieldHost host = new(rejected, (_, _) => 1, initial);
        FieldHostRequest request = new(1, 1, 7, 0x0769, new FieldModelEffectCommand(0, 2) { Payload = [2, 2, 0, 0] });
        FieldHostRequest wait = new(2, 1, 7, 0x0770, new FieldModelEffectCommand(0, Wait: true));
        Check(host.Execute(request).Completed, "Fixed native eye selection must consume its original payload immediately.");
        Check(host.GetActor(1) == actor with { AutomaticBlinkDisabled = true, NativeEyeLeft = 2, NativeEyeRight = 2, NativeEyeControlPending = true }, "Fixed closed eyes must retain the countdown and unrelated actor facts.");
        Check(!host.Execute(wait).Completed, "KAWIW must wait for the queued native eye effect owner.");
        host.AdvanceActors();
        Check(host.GetActor(1).NativeEyeControlPending && !host.Execute(wait).Completed && host.Process.BlinkRandomIndex == 77 && host.GetActor(1).BlinkCountdown == 23, "The strict depth3840 boundary must retain the queued owner without consuming blink RNG or countdown.");
        Ff7FieldProcess frozen = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        Ff7FieldHost original = new(eligible, (_, _) => 1, frozen);
        Ff7FieldHost cold = new(eligible, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(frozen))!);
        original.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold fixed-eye completion must preserve exact queued/material/actor ownership.");
        Check(!original.GetActor(1).NativeEyeControlPending && original.Execute(wait).Completed && original.GetActor(1).NativeEyeLeft == 2 && original.GetActor(1).NativeEyeRight == 2, "One eligible native update must complete KAWIW even for a hidden actor, retaining the fixed closed material selection.");
        Check(original.Process.BlinkRandomIndex == 77 && original.GetActor(1).BlinkCountdown == 23, "Fixed eye material completion cannot consume the independent automatic-blink owner.");
        Check(original.Execute(request with { Id = 3, Command = new FieldModelEffectCommand(0, 1) { Payload = [1, 1, 1, 255] } }).Completed, "A fixed open-eye request must retain native payload byte2 semantics and ignore unused byte3.");
        Check(original.GetActor(1) is { AutomaticBlinkDisabled: true, NativeEyeLeft: 1, NativeEyeRight: 1, BlinkCountdown: 23, NativeEyeControlPending: true }, "Fixed open eyes must disable automatic blinking without resetting the countdown.");
        Console.WriteLine("PASS fixed native eye material, queued KAWIW, depth gate, RNG isolation and cold continuation");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
