using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeEyeControlTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native eye-disable owner: optional installed field resources are unset.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "tin_1");
        Ff7Actor actor = new() { Slot = 3, Entity = 33, Model = 3, Visible = true, Animation = 0, AnimationFrame16 = 0 };
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [actor] });
        FieldHostRequest request = new(1, 33, 7, 0x21D1, new FieldModelEffectCommand(0) { Payload = [0, 0, 0, 255] });
        Check(host.Execute(request).Completed, "Native eye-disable request did not complete immediately.");
        Check(host.GetActor(33) == actor with { AutomaticBlinkDisabled = true, NativeEyeControlPending = true }, "Eye-disable changed another native actor fact.");
        Check(host.Process.Tick == 0, "Eye-disable advanced the process clock.");
        Ff7FieldProcess snapshot = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        Ff7FieldHost cold = new(field, (_, _) => 1, snapshot);
        Check(cold.GetActor(33).AutomaticBlinkDisabled, "Cold restore lost the native disable owner.");
        Check(cold.Execute(JsonSerializer.Deserialize<FieldHostRequest>(JsonSerializer.Serialize(request))!).Completed, "Serialized request lost value-based identity.");
        Check(cold.Execute(request with { Id = 2, Command = new FieldModelEffectCommand(0, 1) { Payload = [1, 1, 0, 0] } }).Completed, "Reached activation did not complete immediately.");
        Check(!cold.GetActor(33).AutomaticBlinkDisabled && cold.GetActor(33).BlinkCountdown == 0 && cold.GetActor(33).NativeEyeLeft == 0 && cold.GetActor(33).NativeEyeRight == 0, "Activation must reset the countdown without an immediate texture replacement.");
        string before = JsonSerializer.Serialize(cold.Capture());
        bool unsupported = false;
        try
        {
            cold.Execute(request with { Id = 3, Command = new FieldModelEffectCommand(0, 2) { Payload = [2, 1, 0, 0] } });
        }
        catch (NotSupportedException)
        {
            unsupported = true;
        }
        Check(unsupported && before == JsonSerializer.Serialize(cold.Capture()), "Unadmitted automatic blink activation changed state.");
        string legacyActor = JsonSerializer.Serialize(actor).Replace("\"AutomaticBlinkDisabled\":false,", "", StringComparison.Ordinal);
        Check(!legacyActor.Contains(nameof(Ff7Actor.AutomaticBlinkDisabled), StringComparison.Ordinal), "The legacy compatibility fixture must omit the new property.");
        Check(!JsonSerializer.Deserialize<Ff7Actor>(legacyActor)!.AutomaticBlinkDisabled, "Absent legacy eye owner must retain the native initial disable flag0.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
