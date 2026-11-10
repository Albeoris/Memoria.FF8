using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeColorOffsetTests
{
    public static void Run()
    {
        Ff7NativeColorOffsetState state = Ff7NativeColorOffset.Request(null, [255, 0, 1, 255, 17, 0, 99]);
        Check(state.Pending && Ff7NativeColorOffset.Shade(state, 0x7F302010) == 0x7F302010, "A queued color offset changed original colors before its native update.");
        Check(Ff7NativeColorOffset.Advance(state, false) == state, "A rejected depth gate advanced native colors.");
        state = Ff7NativeColorOffset.Advance(state, true);
        Check(!state.Pending && Ff7NativeColorOffset.Shade(state, 0x7F302010) == 0x7FFF0021, "Signed RGB offsets, channel saturation or original alpha changed.");
        Check(Ff7NativeColorOffset.Shade(state, 0x7F302010) == 0x7FFF0021, "Repeated presentation accumulated a native offset.");
        state = Ff7NativeColorOffset.Advance(Ff7NativeColorOffset.Request(state, [0, 1, 0, 0, 0, 0]), true);
        Check(state.Red == 255 && state.Green == -255 && state.Blue == 17 && !state.Pending, "An out-of-range native request must complete while preserving all original color offsets.");
        Check(JsonSerializer.Deserialize<Ff7NativeColorOffsetState>(JsonSerializer.Serialize(state)) == state, "Cold color offsets changed.");
        bool rejected = false;
        try
        {
            (state with { Red = 256 }).Validate();
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "Corrupt applied native colors were accepted.");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed native color-offset owner: original field resources are not configured.");
            return;
        }
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root), "nmkin_1");
        Check(field.Scripts.Data.Span.Slice(0x0BAC, 10).SequenceEqual(new byte[] { 0x28, 10, 2, 0, 0, 0, 0, 0, 0, 1 }), "The installed native offset request differs from its authored bytes.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 15360), 0, 1000);
        Ff7Actor actor = new() { Slot = 0, Entity = 11, Model = 0, Visible = false, Z4096 = 10 * 4096 };
        Ff7FieldHost host = new(field with { Cameras = [camera] }, (_, _) => 1, new() { Actors = [actor] });
        FieldHostRequest request = new(1, 11, 1, 0x0BAC, new FieldModelEffectCommand(2, 0) { Payload = [0, 0, 0, 0, 0, 0, 1] });
        FieldHostRequest wait = new(2, 11, 1, 0x0BB6, new FieldModelEffectCommand(13, Wait: true));
        Check(host.Execute(request).Completed && !host.Execute(wait).Completed, "The native request must consume immediately while KAWIW waits for its queued owner.");
        host.AdvanceActors();
        Check(host.GetActor(11).NativeColorOffset is { Pending: true }, "Depth3840 must retain the pending native color owner.");
        Ff7FieldProcess frozen = host.Capture();
        Ff7FieldResources eligible = field with { Cameras = [camera with { Translation = new(0, 0, 15356) }] };
        Ff7FieldHost original = new(eligible, (_, _) => 1, frozen);
        Ff7FieldHost cold = new(eligible, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(frozen))!);
        original.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold pending native colors changed the next callback.");
        Check(original.GetActor(11).NativeColorOffset is { Pending: false, Red: 0, Green: 0, Blue: 0 } && original.Execute(wait).Completed, "One eligible update must complete the original zero-offset request and KAWIW.");
        Check(original.GetActor(11).X4096 == 0 && original.GetActor(11).Z4096 == actor.Z4096, "Native color completion changed gameplay position.");
        Console.WriteLine("PASS native color offset signed operands, saturation, queue, depth gate, original request and cold continuation");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
