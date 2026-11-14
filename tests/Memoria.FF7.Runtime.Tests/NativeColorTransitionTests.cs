using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeColorTransitionTests
{
    public static void Run()
    {
        byte[] payload = Initialization([0, 0, 0], [-5, 7, 0], [2, 3, 0]);
        Ff7NativeColorTransitionState state = Ff7NativeColorTransition.Request(null, payload);
        Check(Ff7NativeColorTransition.Advance(state, false).State == state, "Rejected depth changed a queued transition.");
        short[]? sample;
        (state, sample) = Ff7NativeColorTransition.Advance(state, true);
        Check(sample is null && !state.Pending && !state.Active && state.Step.SequenceEqual(new short[] { -2, 3, 0 }), "Initialization applied colors, changed completion or lost native target-sign step correction.");
        state = Ff7NativeColorTransition.Request(state, [1]);
        short[][] samples = [[0, 0, 0], [-2, 3, 0], [-4, 6, 0], [-5, 7, 0]];
        for (int index = 0; index < samples.Length; index++)
        {
            Ff7NativeColorTransitionState cold = JsonSerializer.Deserialize<Ff7NativeColorTransitionState>(JsonSerializer.Serialize(state))!;
            var expected = Ff7NativeColorTransition.Advance(state, true);
            var restored = Ff7NativeColorTransition.Advance(cold, true);
            Check(JsonSerializer.Serialize(expected.State) == JsonSerializer.Serialize(restored.State) && expected.Sample!.SequenceEqual(restored.Sample!), "Cold transition changed its next native sample.");
            (state, sample) = expected;
            Check(sample!.SequenceEqual(samples[index]) && state.Pending == (index != samples.Length - 1), "Apply-before-increment, signed clamp or final processed callback changed.");
        }
        Check(state.Active && state.Finished, "Completion discarded the persistent native active flag.");
        state = Ff7NativeColorTransition.Request(state, [255]);
        (state, sample) = Ff7NativeColorTransition.Advance(state, true);
        Check(sample!.SequenceEqual(samples[^1]) && !state.Pending, "An active persistent transition incorrectly used a later payload mode.");
        state = Ff7NativeColorTransition.Request(null, [2]);
        (state, sample) = Ff7NativeColorTransition.Advance(state, true);
        Check(sample is null && !state.Pending && !state.Active, "An inactive nonactivation mode incorrectly started a transition.");
        state = Ff7NativeColorTransition.Advance(Ff7NativeColorTransition.Request(null, Initialization([32767, 0, 0], [10, 1, 0], [1, 0, 0])), true).State;
        state = Ff7NativeColorTransition.Advance(Ff7NativeColorTransition.Request(state, [1]), true).State;
        Check(state.Current[0] == short.MinValue && state.Pending && !state.Finished, "Native signed-WORD wrapping or zero-step noncompletion changed.");
        HostLifecycle(payload);
        Installed();
    }

    private static void HostLifecycle(byte[] payload)
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 15356), 0, 200);
        Ff7FieldResources field = new("color-transition-fixture", null!, null!, null!, [camera], [], null!, null!, null!);
        Ff7Actor actor = new() { Slot = 0, Entity = 0, Model = 0, Z4096 = 10 * 4096, Visible = false, NativeColorOffset = new() { Red = 4 } };
        Ff7FieldHost host = new(field, (_, _) => 1, new() { BlinkStateInitialized = true, Actors = [actor] });
        FieldHostRequest initialize = new(1, 0, 7, 0, new FieldModelEffectCommand(6) { Payload = payload });
        FieldHostRequest activate = new(2, 0, 7, 0, new FieldModelEffectCommand(6, 1) { Payload = [1] });
        FieldHostRequest wait = new(3, 0, 7, 0, new FieldModelEffectCommand(0, Wait: true));
        Check(host.Execute(initialize).Completed && !host.Execute(wait).Completed, "KAWAI must consume immediately while KAWIW waits for its queued native owner.");
        host.AdvanceActors();
        Check(host.Execute(wait).Completed && host.GetActor(0).NativeColorOffset!.Red == 4, "Initialization changed applied original colors or failed native completion.");
        host.Execute(activate);
        wait = wait with { Id = 4 };
        host.AdvanceActors();
        Check(!host.Execute(wait).Completed && host.GetActor(0).NativeColorOffset!.Red == 0, "The first transition callback did not apply the initial sample.");
        host.AdvanceActors();
        Check(Ff7NativeColorOffset.Shade(host.GetActor(0).NativeColorOffset!, 0x7F100010) == 0x7F0E0310, "Transition samples did not reach the existing saturating native color consumer with original alpha.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()) && !host.Execute(wait).Completed, "Cold pending final-sample callback changed.");
        host.AdvanceActors();
        Check(host.Execute(wait).Completed && host.GetActor(0).NativeColorOffset!.Red == -5, "Target color did not apply before the native wait completed.");
        Ff7FieldHost far = new(field with { Cameras = [camera with { Translation = new(0, 0, 15360) }] }, (_, _) => 1, new() { BlinkStateInitialized = true, Actors = [actor] });
        far.Execute(initialize);
        far.AdvanceActors();
        Check(far.GetActor(0).NativeColorTransition!.Pending && far.GetActor(0).NativeColorOffset!.Red == 4, "Depth3840 did not suspend initialization on a hidden actor.");
    }

    private static void Installed()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COLOR_TRANSITION_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed color transition: optional resources or external standalone fixture are unset.");
            return;
        }
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(saved))!);
        Console.WriteLine($"Original color transition request: {Convert.ToHexString(session.Field.Scripts.Data.Span.Slice(0x047B, 24))}");
        long tick = session.Host.Process.Tick;
        for (int index = 0; index < 12; index++)
        {
            session.AdvanceTick();
            cold.AdvanceTick();
            Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Original transition continuation differed after cold restore.");
        }
        Check(session.Host.Process.Tick == tick + 12 && session.Host.Process.Actors.Any(actor => actor.NativeColorTransition is not null), "Original transition fixture did not reach its authored request.");
        Check(File.ReadAllBytes(path).SequenceEqual(original), "Validation changed the standalone fixture.");
        Console.WriteLine($"PASS original color transition continuation: field={session.Field.Name} tick={session.Host.Process.Tick}");
    }

    private static byte[] Initialization(short[] current, short[] target, short[] step)
    {
        byte[] payload = new byte[21];
        for (int channel = 0; channel < 3; channel++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2 + channel * 2), current[channel]);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8 + channel * 2), target[channel]);
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(14 + channel * 2), step[channel]);
        }
        return payload;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
