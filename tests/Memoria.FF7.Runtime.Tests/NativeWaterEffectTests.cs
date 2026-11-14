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
        Ff7NativeWaterEffectState nativeZero = Ff7NativeWaterEffect.Request(null, [1], 0);
        Check(nativeZero is { Active: true, Initialized: true, Parameter0: 0, Parameter1: 0, Parameter2: 0, Parameter3: 0, Flags: 0, CycleIndex: 0, SubcycleIndex: 0 }, "Mode1 without a preceding mode0 must resolve the native zero-initialized model cache.");
        Ff7NativeWaterEffectSample zeroSample = new(new(0, 0, 0, 0, 0, 0), [], 0, 0);
        Ff7NativeWaterEffectState zeroCold = JsonSerializer.Deserialize<Ff7NativeWaterEffectState>(JsonSerializer.Serialize(nativeZero))!;
        Check(JsonSerializer.Serialize(Ff7NativeWaterEffect.Advance(nativeZero, true, zeroSample)) == JsonSerializer.Serialize(Ff7NativeWaterEffect.Advance(zeroCold, true, zeroSample)), "Native zero-cache application changed across a cold round trip.");
        Ff7Actor legacyActor = JsonSerializer.Deserialize<Ff7Actor>("{\"Slot\":0,\"Entity\":13,\"Model\":8}")!;
        Check(JsonSerializer.Serialize(Ff7NativeWaterEffect.Request(legacyActor.NativeWaterEffect, [1], legacyActor.Slot)) == JsonSerializer.Serialize(nativeZero), "An older actor with absent nonprogress cache must resolve native zeros without replay or a capability marker.");
        Throws<InvalidDataException>(() => Ff7NativeWaterEffect.Request(state, Payload(), 7));
        RunApplication(state);
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
        Throws<NotSupportedException>(() => host.Execute(new(6, 13, 7, 0x0C46, new FieldModelEffectCommand(8, 2) { Payload = new byte[] { 2 } })));
        Check(beforeUnsupported == JsonSerializer.Serialize(host.Capture()), "An unsupported water mode partially changed state.");
        RunSelector9(field);
        RunSplashQueues(field, state: cached);
        RunCheckpoint(installation);
        RunAliasCheckpoint(installation);
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
        Check(save.FieldName == "ujunon2" && save.Vm.Tick is 0 or 1, "The configured water-effect fixture must retain the reached beach initializer.");
        using Ff7Session live = Ff7Session.Restore(installation, save);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!);
        live.AdvanceTick(advanceClock: false);
        cold.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The actual beach initializer diverged from its independent cold callback.");
        Check(live.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == 13).NativeWaterEffect is { Active: true }, "The original dolphin initializer did not retain its native mode0 owner.");
        if (save.Vm.Tick == 0)
        {
            live.AdvanceTick(advanceClock: false);
            cold.AdvanceTick(advanceClock: false);
            Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The original mode1 callback diverged from its independent cold continuation.");
        }
        Check(live.Machine.Capture().Tick == 2 && cold.Machine.Capture().Tick == 2, "The bounded initializer continuation must reach native tick2.");
        Ff7NativeWaterEffectState applied = live.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == 13).NativeWaterEffect ?? throw new InvalidDataException("The original dolphin lost its native water cache.");
        Ff7NativeWaterEffectSample displayed = applied.DisplayedSample ?? throw new InvalidDataException("The original dolphin did not capture its mode1 color sample.");
        Check(applied is { OwnsColors: true, SubcycleIndex: 2 } && displayed.Parameters.EnvironmentProfile == 3, "The original mode1 callback did not capture its cached profile3 water colors and preincrement counters.");
        Check(displayed.CycleIndex == 0 && displayed.SubcycleIndex == 1 && displayed.Parts.Length == live.Models[8].Geometry.Count, "The original dolphin's accepted-callback masks differ from its source topology.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The water-effect standalone changed during read-only continuation.");
    }

    private static void RunAliasCheckpoint(Ff7Installation installation)
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WATER_ALIAS_SAVE");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP reached shared-water standalone: MEMORIA_FF7_WATER_ALIAS_SAVE is not configured.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The shared-water fixture has no FFVII continuation.");
        Check(save.FieldName == "ujunon2" && save.Vm.Tick == 1020, "The configured shared-water fixture must retain the reached native callback1020 appearance.");
        using Ff7Session live = Ff7Session.Restore(installation, save);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(save))!);
        Ff7NativeWaterEffectState before = live.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == 12).NativeWaterEffect ?? throw new InvalidDataException("The reached selector9 actor has no recorded shared native cache.");
        Check(before is { Active: true, OwnsColors: true, Parameter0: -7, Parameter1: 0, Parameter2: 0, Parameter3: 128, Flags: 0, DisplayedSample: not null }, "The selected appearance fixture must preserve its actual completed selector9 cache and color sample.");
        live.AdvanceTick(advanceClock: false);
        cold.AdvanceTick(advanceClock: false);
        Check(JsonSerializer.Serialize(live.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The actual selector9 continuation diverged from its independent cold callback.");
        Check(live.Machine.Capture().Tick == 1021 && cold.Machine.Capture().Tick == 1021, "The bounded selector9 continuation must reach native callback1021.");
        Ff7NativeWaterEffectState water = live.Host.Process.Actors.Single(actor => actor.EntityBound && actor.Entity == 12).NativeWaterEffect ?? throw new InvalidDataException("The reached selector9 actor lost its shared native cache.");
        Check(water is { Active: true, OwnsColors: true, Parameter0: -7, Parameter1: 0, Parameter2: 0, Parameter3: 128, Flags: 0, DisplayedSample: not null } && water.RequestedPayload.SequenceEqual(new byte[] { 1 }), "The original selector9 application did not preserve its initialized four-word cache and displayed color ownership.");
        Check(live.Host.Process.NativeWaterSplashCursor is >= 0 and < 4 && live.Host.Process.NativeWaterSplashCursor == cold.Host.Process.NativeWaterSplashCursor, "The original shared-water cursor lost its global cold continuation.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The shared-water standalone changed during read-only continuation.");
    }

    private static void RunSelector9(Ff7FieldResources field)
    {
        byte[] payload = [0, 0, 0xF9, 0xFF, 0, 0, 0, 0, 128, 0, 0];
        Check(field.Scripts.Data.Span.Slice(0x0BD4, 14).SequenceEqual(new byte[] { 0x28, 14, 9, 0, 0, 0xF9, 0xFF, 0, 0, 0, 0, 128, 0, 0 }) && field.Scripts.Data.Span.Slice(0x0BE2, 7).SequenceEqual(new byte[] { 0x24, 1, 0, 0x28, 4, 9, 1 }), "The original reached selector9 initialization/application operands changed.");
        Ff7FieldHost Configure(Ff7FieldProcess process)
        {
            return new(field, (_, _) => 1, process, 429)
            {
                NativeWaterEnvironmentProfile = (_, _) => 3,
                NativeWaterGeometryCapture = (_, _, _) => new([new(0, [true, false])], [])
            };
        }
        Ff7FieldHost host = Configure(new() { Actors = [new() { Slot = 8, Entity = 13, Model = 8 }] });
        Check(host.Execute(new(30, 13, 7, 0x0BD4, new FieldModelEffectCommand(9) { Payload = payload })).Completed, "Selector9 initialization must dispatch immediately through the shared water owner.");
        host.AdvanceActors();
        Check(host.GetActor(13).NativeWaterEffect is { Active: true, Parameter0: -7, Parameter1: 0, Parameter2: 0, Parameter3: 128, Flags: 0, CycleIndex: 0, SubcycleIndex: 1 }, "Selector9 must process its eleven-byte payload using the existing four-word cache and counters.");
        Check(host.Execute(new(31, 13, 7, 0x0BE5, new FieldModelEffectCommand(9, 1) { Payload = [1] })).Completed, "Selector9 application did not reuse its cache.");
        Ff7FieldHost cold = Configure(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()) && host.GetActor(13).NativeWaterEffect is { Active: true, OwnsColors: true, SubcycleIndex: 2 }, "Selector9 must use the shared application callback and exact cold counters without cancelling itself.");
        Check(!host.Execute(new(32, 13, 7, 0, new FieldModelEffectCommand(13, Wait: true))).Completed, "Selector9 must retain the same persistent KAWIW owner as selector8.");
        Check(host.Execute(new(33, 13, 7, 0, new FieldModelEffectCommand(8, 1) { Payload = [1] })).Completed && host.GetActor(13).NativeWaterEffect is { Active: true, Parameter0: -7 }, "Switching between selectors8/9 must preserve the same physical native cache and active callback.");
        Check(host.Execute(new(34, 13, 7, 0, new FieldModelEffectCommand(2) { Payload = new byte[6] })).Completed && host.GetActor(13).NativeWaterEffect is { Active: false, OwnsColors: true }, "A later different selector must cancel the shared8/9 owner while retaining its cached display.");
    }

    private static void RunApplication(Ff7NativeWaterEffectState initialized)
    {
        Ff7NativeWaterEffectState state = Ff7NativeWaterEffect.Request(initialized, [1], initialized.ModelSlot);
        Check(state.Parameter0 == initialized.Parameter0 && state.RequestedPayload.SequenceEqual(new byte[] { 1 }) && state.DisplayedSample is null, "Mode1 must select the initialized physical cache without reading nonexistent parameter operands.");
        for (int callback = 0; callback < 12; callback++)
        {
            short cycle = state.CycleIndex;
            short subcycle = state.SubcycleIndex;
            Ff7NativeWaterEffectSample sample = new(new(state.Parameter0, state.Parameter1, state.Parameter2, state.Parameter3, state.Flags, 3), [new(0, [true, false, true])], cycle, subcycle);
            Check(ReferenceEquals(state, Ff7NativeWaterEffect.Advance(state, false, sample)), "An ineligible water application consumed a color sample or counter.");
            Ff7NativeWaterEffectState cold = JsonSerializer.Deserialize<Ff7NativeWaterEffectState>(JsonSerializer.Serialize(state))!;
            state = Ff7NativeWaterEffect.Advance(state, true, sample);
            Check(state.DisplayedSample == sample && state.OwnsColors && state.Active && state.SubcycleIndex == (subcycle + 1) % 3 && state.CycleIndex == (cycle + (subcycle == 2 ? 1 : 0)) % 4, "Mode1 must sample before advancing its persistent modulo3/modulo4 counters.");
            Check(JsonSerializer.Serialize(state) == JsonSerializer.Serialize(Ff7NativeWaterEffect.Advance(cold, true, sample)), "Mode1 cache/masks changed after a cold round trip.");
        }
        Check(state.CycleIndex == 0 && state.SubcycleIndex == 1, "Twelve callbacks failed to return the native counter phase.");
        Ff7NativeWaterEffectState reset = Ff7NativeWaterEffect.Advance(Ff7NativeWaterEffect.Request(state, Payload(), state.ModelSlot), true);
        Check(reset.OwnsColors && reset.DisplayedSample == state.DisplayedSample && reset.CycleIndex == 0 && reset.SubcycleIndex == 1, "Mode0's matrix prepass must preserve earlier applied colors and their sample.");
        Throws<InvalidDataException>(() => Ff7NativeWaterEffect.Advance(Ff7NativeWaterEffect.Request(state, [1], state.ModelSlot), true));
    }

    private static void RunSplashQueues(Ff7FieldResources field, Ff7NativeWaterEffectState state)
    {
        Ff7Actor later = new() { Slot = 4, Entity = 14, Model = 8, Visible = false, NativeWaterEffect = Ff7NativeWaterEffect.Request(state with { ModelSlot = 4 }, [1], 4) };
        Ff7Actor first = new() { Slot = 2, Entity = 13, Model = 8, NativeWaterEffect = Ff7NativeWaterEffect.Request(state with { ModelSlot = 2 }, [1], 2) };
        Ff7FieldHost Configure(Ff7FieldProcess process)
        {
            return new(field, (_, _) => 1, process, 429)
            {
                NativeWaterEnvironmentProfile = (_, _) => 3,
                NativeWaterSplashResources = [true, true, true, true],
                NativeWaterGeometryCapture = (actor, _, _) => new([new(0, [true, false])], Enumerable.Range(0, 6).Select(bone => new Ff7FieldWaterSplashCandidate(bone, actor.Slot + 0.25f, bone + 0.5f, 0.75f, 12)).ToArray())
            };
        }
        Ff7FieldHost host = Configure(new() { Actors = [later, first], NativeWaterSplashCursor = 3 });
        host.AdvanceActors();
        Check(host.Process.NativeWaterSplashes.Length == 12 && host.Process.NativeWaterSplashCursor == 3, "Four texture queues must retain all twelve candidates rather than impose a four-splash cap.");
        Check(host.Process.NativeWaterSplashes.Take(6).All(splash => splash.ModelSlot == 2) && host.Process.NativeWaterSplashes.Skip(6).All(splash => splash.ModelSlot == 4), "Water allocation must follow physical model-slot order even for hidden models.");
        Check(host.Process.NativeWaterSplashes.Select(splash => splash.TextureIndex).SequenceEqual(Enumerable.Range(0, 12).Select(index => (3 + index) % 4)), "Water texture selection must use the global cursor rather than per-model cache counters.");
        Ff7FieldProcess coldProcess = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(coldProcess), "Water masks and explicit floating coordinates must round-trip exactly.");
        Ff7FieldHost cold = Configure(coldProcess);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()) && host.Process.NativeWaterSplashes.Length == 12, "Cold splash capture must replace the callback list without accumulating earlier quads.");
        Ff7FieldProcess next = host.CreateNextFieldProcess(429);
        Check(next.NativeWaterSplashCursor == 3 && next.NativeWaterSplashes.Length == 0, "Field changes must preserve the native BSS cursor but discard field-local queued primitives.");
        host.NativeWaterSplashResources = [true, true, true, false];
        host.AdvanceActors();
        Check(host.Process.NativeWaterSplashes.Length == 0 && host.Process.NativeWaterSplashCursor == 3, "A null selected resource must skip allocation without advancing the cursor.");
        Check(host.Execute(new(20, 13, 7, 0, new FieldModelEffectCommand(2) { Payload = new byte[6] })).Completed, "A color writer could not replace the native water owner.");
        Check(host.GetActor(13).NativeWaterEffect is { Active: false, OwnsColors: true }, "Requesting another owner must preserve already applied water colors until an eligible write.");
        host.AdvanceActors();
        Check(host.GetActor(13).NativeWaterEffect is { Active: false, OwnsColors: false, DisplayedSample: not null }, "An actual eligible color write must replace water color ownership while retaining cached facts.");
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
