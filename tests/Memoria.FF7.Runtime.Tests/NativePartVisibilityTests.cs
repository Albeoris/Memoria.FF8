using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePartVisibilityTests
{
    public static void Run()
    {
        byte[] hidden = [7, 0, 0, 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0];
        Ff7NativePartVisibilityState requested = Ff7NativePartVisibility.Request(null, hidden, 7);
        Check(!requested.Completed && requested.Visibility.All(value => value == 1), "A request changed native visibility before its eligible callback.");
        Check(ReferenceEquals(requested, Ff7NativePartVisibility.Advance(requested, false, 186)), "An ineligible native effect callback changed its continuation.");
        Ff7NativePartVisibilityState cold = Cold(requested);
        Ff7NativePartVisibilityState applied = Ff7NativePartVisibility.Advance(cold, true, 186);
        Check(applied.Completed && applied.Visibility.All(value => value == 0), "The actual seven-part command did not hide every selected original part.");
        Check(Cold(applied).Visibility.SequenceEqual(applied.Visibility), "Cold restoration changed applied part flags.");
        Ff7NativePartVisibilityState partial = Ff7NativePartVisibility.Advance(Ff7NativePartVisibility.Request(applied, [2, 0, 1, 6, 0], 7), true, 186);
        Check(partial.Visibility.SequenceEqual(new byte[] { 1, 1, 1, 1, 1, 1, 0 }), "A partial request failed to rebuild the native default-one work table.");
        Ff7NativePartVisibilityState special = Ff7NativePartVisibility.Advance(Ff7NativePartVisibility.Request(null, [2, 0, 255, 6, 0], 7), true, 461);
        Check(special.Visibility.SequenceEqual(new byte[] { 1, 0, 0, 0, 0, 0, 0 }), "The original field profile did not begin with its native default-zero mask.");
        Ff7NativePartVisibilityState ignored = Ff7NativePartVisibility.Advance(Ff7NativePartVisibility.Request(null, [3, 255, 0, 2, 0, 2, 128], 7), true, 186);
        Check(ignored.Visibility.All(value => value == 1), "Invalid part indices were not ignored, or the last raw nonzero flag did not win.");
        Throws<InvalidDataException>(() => Ff7NativePartVisibility.Request(null, [2, 0, 1], 7));
        Throws<InvalidDataException>(() => Ff7NativePartVisibility.Request(applied, [0], 6));
        Throws<NotSupportedException>(() => Ff7NativePartVisibility.Request(null, [0], 101));
        Throws<InvalidDataException>(() => (requested with { Visibility = [2] }).Validate());
        RunHost(hidden);
    }

    private static void RunHost(byte[] hidden)
    {
        Ff7FieldHost host = Build();
        FieldHostRequest request = new(1, 13, 7, 0x139A, new FieldModelEffectCommand(10, 7) { Payload = hidden });
        Check(host.Execute(request).Completed, "KAWAI10 unexpectedly retained the script request.");
        Check(!host.Process.Actors[0].PartVisibility!.Completed, "The model callback was skipped when the script request completed.");
        FieldHostRequest wait = new(2, 13, 7, 0x13AC, new FieldModelEffectCommand(13, Wait: true));
        Check(!host.Execute(wait).Completed, "KAWAIW completed before the native model callback.");
        Ff7FieldHost cold = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.AdvanceActors();
        Check(cold.Process.Actors[0].PartVisibility is { Completed: true } state && state.Visibility.All(value => value == 0), "Cold pending owner did not apply the exact seven-part command.");
        Check(cold.Execute(wait).Completed, "KAWAIW failed to complete after the one-shot callback.");
        Ff7FieldHost restored = Build(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(cold.Capture()))!);
        Check(restored.Process.Actors[0].PartVisibility!.Visibility.All(value => value == 0), "KAWAIW removed native persistent part flags.");
        Ff7FieldHost far = Build(cameraDepth: 15360);
        far.Execute(request);
        far.AdvanceActors();
        Check(far.Process.Actors[0].PartVisibility is { Completed: false }, "The strict native depth3840 gate did not suspend its owner.");
    }

    private static Ff7FieldHost Build(Ff7FieldProcess? saved = null, int cameraDepth = 1000)
    {
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, new Vector3(0, 0, cameraDepth), 0, 100);
        Ff7FieldResources field = new("part-visibility-fixture", null!, null!, null!, [camera], [], null!, null!, null!);
        Ff7FieldProcess process = saved ?? new() { BlinkStateInitialized = true, NativeMusicTrack = 0, Actors = [new() { Slot = 0, Entity = 13, Model = 0 }] };
        return new(field, (_, _) => 1, process, fieldId: 186) { ModelPartVertexCounts = _ => [1, 1, 1, 1, 1, 1, 1] };
    }

    private static Ff7NativePartVisibilityState Cold(Ff7NativePartVisibilityState state)
    {
        Ff7NativePartVisibilityState result = JsonSerializer.Deserialize<Ff7NativePartVisibilityState>(JsonSerializer.Serialize(state))!;
        result.Validate();
        return result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
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
        throw new Exception("Expected " + typeof(T).Name);
    }
}
