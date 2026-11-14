using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWaterEffectTests
{
    public static void Run()
    {
        byte[] payload = Payload();
        Ff7NativeWaterEffectState request = Ff7NativeWaterEffect.Request(null, payload, 8);
        payload[2] = 99;
        Check(request.Active && !request.Initialized && request.ModelSlot == 8 && request.RequestedPayload[2] == 224, "The mode0 request must retain its original payload and physical model owner without initializing the cache.");
        Check(ReferenceEquals(request, Ff7NativeWaterEffect.Advance(request, false)), "An ineligible mode0 request advanced its cache.");
        Ff7NativeWaterEffectState state = Ff7NativeWaterEffect.Advance(request, true);
        Check(state.Initialized && state.Active && state.Parameter0 == -32 && state.Parameter1 == -64 && state.Parameter2 == -32 && state.Parameter3 == 0 && state.Flags == 224 && state.CycleIndex == 0 && state.SubcycleIndex == 1, "Mode0 cache words, flag, persistent owner or common-return counters differ from native.");
        Check(state.RequestedPayload[1] == 0 && state.ModelSlot == 8, "The original payload must remain separate from the native processing model-index write.");
        for (int callback = 0; callback < 8; callback++)
        {
            state = Ff7NativeWaterEffect.Advance(state, true);
            Check(state.CycleIndex == 0 && state.SubcycleIndex == 1 && state.Active, "Repeated mode0 processing must reset counters before the common return, without completing its owner.");
        }
        Ff7NativeWaterEffectState cold = JsonSerializer.Deserialize<Ff7NativeWaterEffectState>(JsonSerializer.Serialize(state))!;
        Check(JsonSerializer.Serialize(Ff7NativeWaterEffect.Advance(cold, true)) == JsonSerializer.Serialize(Ff7NativeWaterEffect.Advance(state, true)), "Cold mode0 processing changed its cache/counters.");
        byte[] changed = Payload();
        BinaryPrimitives.WriteInt16LittleEndian(changed.AsSpan(2), 123);
        Ff7NativeWaterEffectState replacement = Ff7NativeWaterEffect.Request(state, changed, 8);
        Check(replacement.Parameter0 == -32 && Ff7NativeWaterEffect.Advance(replacement, true).Parameter0 == 123, "A replacement request must retain the cached words until the eligible processing phase.");
        Ff7NativeWaterEffectState cancelled = state with { Active = false };
        Check(ReferenceEquals(cancelled, Ff7NativeWaterEffect.Advance(cancelled, true)), "A cancelled owner changed its retained cache.");
        Throws<InvalidDataException>(() => Ff7NativeWaterEffect.Request(null, new byte[10], 0));
        Throws<InvalidDataException>(() => Ff7NativeWaterEffect.Request(null, Payload(), 255));
        Throws<NotSupportedException>(() => Ff7NativeWaterEffect.Request(null, new byte[] { 1 }, 0));
        Throws<InvalidDataException>(() => Ff7NativeWaterEffect.Request(state, Payload(), 7));
        RunInstalled();
        Console.WriteLine("PASS native KAWAI8 mode0 cache, physical model ownership, depth eligibility, persistent wait, cancellation and cold state");
    }

    private static void RunInstalled()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original dolphin KAWAI8: MEMORIA_FF7_GAME_DIR is not configured.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon2");
        Check(field.Scripts.Data.Span.Slice(0x0C22, 3).SequenceEqual(new byte[] { 0x28, 30, 8 }) && field.Models.Models[8].Hrc.Equals("BDCD.HRC", StringComparison.OrdinalIgnoreCase), "The reached original dolphin effect/binding changed.");
        byte[] payload = field.Scripts.Data.Span.Slice(0x0C25, 27).ToArray();
        Check(payload.SequenceEqual(Payload()), "The reached original mode0 payload differs from the reviewed cache words and retained trailing data.");
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 4096), 0, 256);
        field = field with { Cameras = [camera] };
        Ff7Actor hidden = new() { Slot = 0, Entity = 13, Model = 8, Visible = false };
        Ff7Actor distant = new() { Slot = 1, Entity = 14, Model = 8, Z4096 = 20000 * 4096 };
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [hidden, distant] }, 429);
        FieldHostRequest first = new(1, 13, 7, 0x0C22, new FieldModelEffectCommand(8) { Payload = payload });
        FieldHostRequest second = new(2, 14, 7, 0x0C22, new FieldModelEffectCommand(8) { Payload = payload });
        Check(host.Execute(first).Completed && host.Execute(second).Completed, "KAWAI8 dispatch must complete without waiting for the model pass.");
        Check(host.Process.Actors[0].NativeWaterEffect is { ModelSlot: 0, Initialized: false } && host.Process.Actors[1].NativeWaterEffect is { ModelSlot: 1, Initialized: false }, "Repeated descriptors must retain separate physical model caches.");
        host.AdvanceActors();
        Check(host.Process.Actors[0].NativeWaterEffect is { Initialized: true, Active: true } && host.Process.Actors[1].NativeWaterEffect is { Initialized: false, Active: true }, "Native processing must ignore visibility but retain the projected-depth3840 gate.");
        Check(host.Process.Actors[0].ModelEffect is null && host.Process.Actors[0].NativeColorOffset is null && host.Process.Actors[0].NativeColorTransition is null && host.Process.Actors[0].PartVisibility is null, "Mode0 cache initialization invented a color, topology or lighting effect.");
        Check(!host.Execute(new(3, 13, 7, 0x0C40, new FieldModelEffectCommand(13, Wait: true))).Completed, "KAWIW must remain pending for native mode0's persistent owner.");
        Ff7FieldHost restored = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!, 429);
        host.AdvanceActors();
        restored.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Cold dolphin processing changed the exact next callback.");
        Ff7NativeWaterEffectState cached = host.Process.Actors[0].NativeWaterEffect!;
        Check(host.Execute(new(4, 13, 7, 0x0C42, new FieldModelEffectCommand(2) { Payload = new byte[6] })).Completed, "A later supported model selector failed to replace the water owner.");
        Check(host.Process.Actors[0].NativeWaterEffect is { Active: false } && (host.Process.Actors[0].NativeWaterEffect! with { Active = true }) == cached && host.Process.Actors[1].NativeWaterEffect is { Active: true }, "Replacement must cancel only the selected physical owner and preserve its exact cached facts.");
        host.AdvanceActors();
        Check(host.Execute(new(5, 13, 7, 0x0C44, new FieldModelEffectCommand(13, Wait: true))).Completed, "KAWIW did not complete after its later single effect owner finished.");
        string beforeUnsupported = JsonSerializer.Serialize(host.Capture());
        Throws<NotSupportedException>(() => host.Execute(new(6, 13, 7, 0x0C46, new FieldModelEffectCommand(8, 1) { Payload = new byte[] { 1 } })));
        Check(beforeUnsupported == JsonSerializer.Serialize(host.Capture()), "An unsupported water mode partially changed state.");
        RunCheckpoint(installation);
    }

    private static void RunCheckpoint(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WATER_EFFECT_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP reached water-effect standalone: MEMORIA_FF7_WATER_EFFECT_SAVE is not configured.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The water-effect fixture has no FFVII continuation.");
        Check(save.FieldName == "ujunon2" && save.Vm.Tick == 0, "The configured water-effect fixture must retain the reached beach initializer.");
        using Ff7Session live = Ff7Session.Restore(installation, save);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!);
        live.AdvanceTick(advanceClock: false);
        cold.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The actual beach initializer diverged from its independent cold callback.");
        Check(live.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == 13).NativeWaterEffect is { Active: true }, "The original dolphin initializer did not retain its native mode0 owner.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The water-effect standalone changed during read-only continuation.");
    }

    private static byte[] Payload() => [0, 0, 0xE0, 0xFF, 0xC0, 0xFF, 0xE0, 0xFF, 0, 0, 0xE0, 0xFF, 0xC0, 0xFF, 0xE0, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2];

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
