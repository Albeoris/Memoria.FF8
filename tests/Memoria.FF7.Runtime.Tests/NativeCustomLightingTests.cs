using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCustomLightingTests
{
    internal static void Run()
    {
        byte[] payload = Initialization();
        Ff7NativeCustomLightingState pending = Ff7NativeCustomLighting.Request(null, payload, 3);
        payload[2] = 99;
        Check(pending.Pending && pending.Cached is null && pending.RequestedPayload[2] == 0 && pending.RequestedPayload[^1] == 85, "Custom lighting borrowed or normalized its complete request.");
        Check(ReferenceEquals(pending, Ff7NativeCustomLighting.Advance(pending, false)), "Ineligible custom lighting consumed its request.");
        Ff7NativeCustomLightingState initialized = Ff7NativeCustomLighting.Advance(pending, true);
        Check(!initialized.Pending && !initialized.Enabled && initialized.Displayed is null && initialized.Cached!.Colors.SequenceEqual(pending.RequestedPayload.Skip(2).Take(12)), "Mode0 did not cache its12 raw color bytes and complete independently of application.");
        Check(initialized.Cached!.Directions.SequenceEqual(new short[] { short.MinValue, short.MaxValue, -1, 0, 4096, -4096, 385, -336, 43 }), "Custom lighting changed signed little-endian direction words.");
        Ff7NativeCustomLightingState application = Ff7NativeCustomLighting.Request(initialized, [1, 177], 3);
        Check(application.Pending && !application.Enabled && application.RequestedPayload.SequenceEqual(new byte[] { 1, 177 }), "Mode1 consumed cached words from following bytes or applied during dispatch.");
        Ff7NativeCustomLightingState applied = Ff7NativeCustomLighting.Advance(application, true);
        Check(applied is { Enabled: true, Pending: true, Displayed: not null } && applied.Displayed.Colors.SequenceEqual(initialized.Cached.Colors) && !ReferenceEquals(applied.Displayed.Colors, initialized.Cached.Colors), "Mode1 lost its persistent owner or aliased the raw cache.");
        Check(Ff7NativeCustomLighting.Advance(Ff7NativeCustomLighting.Request(applied, [17], 3), true) is { Pending: true, Enabled: true }, "An enabled native cache incorrectly stopped on another nonzero request mode.");
        Check(Ff7NativeCustomLighting.Advance(Ff7NativeCustomLighting.Request(initialized, [17], 3), true) is { Pending: false, Enabled: false }, "An inactive noninitializing mode invented a persistent application.");
        Ff7NativeCustomLightingState cold = JsonSerializer.Deserialize<Ff7NativeCustomLightingState>(JsonSerializer.Serialize(application))!;
        Check(JsonSerializer.Serialize(Ff7NativeCustomLighting.Advance(cold, true)) == JsonSerializer.Serialize(applied), "Cold raw custom lighting changed its next model callback.");
        Throws<InvalidDataException>(() => Ff7NativeCustomLighting.Request(null, [], 0));
        Throws<InvalidDataException>(() => Ff7NativeCustomLighting.Request(null, new byte[31], 0));
        Throws<InvalidDataException>(() => Ff7NativeCustomLighting.Request(initialized, [1], 4));
        Throws<NotSupportedException>(() => Ff7NativeCustomLighting.Advance(Ff7NativeCustomLighting.Request(null, [1], 0), true));
        Ff7Actor legacy = JsonSerializer.Deserialize<Ff7Actor>("{\"Slot\":0,\"Entity\":0,\"Model\":0}")!;
        Check(legacy.NativeCustomLighting is null && !JsonSerializer.Serialize(legacy).Contains("NativeCustomLighting", StringComparison.Ordinal), "Optional lighting metadata changed an old actor's native JSON/hash.");
        CheckHost();
        Console.WriteLine("PASS custom-lighting raw cache widths, eligibility, physical ownership, persistent wait, replacement and cold/default metadata");
    }

    private static void CheckHost()
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 4096), 0, 256);
        Ff7FieldResources field = NativeDialogueTests.Field() with { Cameras = [camera] };
        Ff7Actor hidden = new() { Slot = 0, Entity = 0, Model = 0, Visible = false, AutomaticBlinkDisabled = true };
        Ff7Actor distant = new() { Slot = 1, Entity = 1, Model = 0, Z4096 = 20000 * 4096, Visible = false, AutomaticBlinkDisabled = true };
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Actors = [hidden, distant] });
        FieldModelEffectCommand command = new(7) { Payload = Initialization() };
        Check(host.Execute(new(1, 0, 7, 10, command)).Completed && host.Execute(new(2, 1, 7, 10, command)).Completed, "Custom-lighting dispatch retained the script instead of queuing physical model processing.");
        FieldHostRequest wait = new(3, 0, 7, 47, new FieldModelEffectCommand(13, Wait: true));
        Check(!host.Execute(wait).Completed, "KAWIW completed before the native model callback.");
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceActors();
        cold.AdvanceActors();
        Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold host lighting changed its first eligible callback.");
        Check(host.Process.Actors[0].NativeCustomLighting is { Pending: false, ModelSlot: 0, Cached: not null } && host.Process.Actors[1].NativeCustomLighting is { Pending: true, ModelSlot: 1, Cached: null } && host.Execute(wait).Completed, "Physical model slots, hidden eligibility or depth-gated cache ownership were merged.");
        host.Execute(new(4, 0, 7, 48, new FieldModelEffectCommand(7, 1) { Payload = [1] }));
        host.AdvanceActors();
        Check(!host.Execute(new(5, 0, 7, 52, new FieldModelEffectCommand(13, Wait: true))).Completed, "Persistent raw mode1 application falsely completed its KAWIW.");
        host.Execute(new(6, 0, 7, 53, new FieldModelEffectCommand(1) { Payload = [0] }));
        Check(host.Process.Actors[0].NativeCustomLighting is { Pending: false, Enabled: true, Cached: not null } && host.Process.Actors[1].NativeCustomLighting!.Pending, "A replacement erased the cache or canceled a different physical owner.");
        host.AdvanceActors();
        Check(host.Execute(new(7, 0, 7, 57, new FieldModelEffectCommand(13, Wait: true))).Completed, "A replacing completed selector retained the previous custom-lighting request.");
        host.Capture().Validate();
    }

    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CUSTOM_LIGHTING_SAVE") ?? throw new InvalidOperationException("External partial-initialization fixture required.");
        byte[] source = File.ReadAllBytes(path);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using MemoryStream stream = new(source, false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session normal = Ff7Session.Restore(installation, saved);
        using Ff7Session cold = Ff7Session.Restore(installation, saved);
        FieldHostRequest request = saved.Vm.PendingRequests!.Single(request => request.Command is FieldModelEffectCommand { Operation: 7, Wait: false });
        Ff7Actor actor = saved.Process.Actors.Single(actor => actor.Entity == request.Entity);
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(normal);
        short story = normal.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        Check(before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(cold), "Exact partial initialization cold restore changed native state.");
        bool producerCompleted = false;
        bool replayedOwnerInitialization = false;
        Ff7NativeTickObservation? completedTick = null;
        normal.NativeTickCompleted += observation => completedTick = observation;
        normal.Host.NativeOperationExecuted += (observed, reply) =>
        {
            replayedOwnerInitialization |= observed.Entity == request.Entity && observed.ProgramCounter < request.ProgramCounter;
            if (observed.Id == request.Id)
            {
                Check(observed.Entity == request.Entity && observed.Priority == request.Priority && observed.ProgramCounter == request.ProgramCounter && reply.Completed, "The restored producer changed its exact pending request owner.");
                producerCompleted = true;
            }
        };
        normal.AdvanceTick();
        cold.AdvanceTick();
        Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(normal) == Ff7AutoplayDriver.ReadNativeRouteFingerprint(cold), "Partial initialization cold continuation changed its native callback.");
        Ff7NativeCustomLightingState state = normal.Host.GetActor(request.Entity).NativeCustomLighting ?? throw new InvalidOperationException("The exact pending custom-lighting owner was not restored.");
        Check(state.ModelSlot == actor.Slot && state.Cached is not null && !state.Pending && producerCompleted, "Restored mode0 lost its physical cache, source PC or exact pending request receipt.");
        Check(normal.Machine.Capture().Entities[request.Entity].ProgramCounter == request.ProgramCounter + ((FieldModelEffectCommand)request.Command).Payload!.Length + 3 && normal.Machine.Capture().Entities[request.Entity].WaitCounter == 1 && normal.FieldGeneration == saved.FieldGeneration && normal.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == story, $"Mode0 continuation differs from the expected callback boundary: PC={normal.Machine.Capture().Entities[request.Entity].ProgramCounter:X}, wait={normal.Machine.Capture().Entities[request.Entity].WaitCounter}, priority={normal.Machine.Capture().Entities[request.Entity].Priority}, field={normal.FieldGeneration}, story={normal.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0)}, tick={normal.Host.Process.Tick}.");
        Check(!replayedOwnerInitialization && completedTick is not null && saved.TickContinuation!.ScriptActivity!.Where(activity => activity.Dispatch is not null).All(activity => completedTick.ScriptActivity!.Contains(activity)), "The partial native tick replayed initialization or discarded its saved accepted child dispatch.");
        Check(source.SequenceEqual(File.ReadAllBytes(path)), "The authoritative partial-initialization source changed.");
        Console.WriteLine($"PASS original partial initialization mode0 request:{request.Id}, entity:{request.Entity}, slot:{actor.Slot}, sourcePC:{request.ProgramCounter:X}, tick:{normal.Host.Process.Tick}, nextPC:{normal.Machine.Capture().Entities[request.Entity].ProgramCounter:X}, retainedDispatches:{saved.TickContinuation!.ScriptActivity!.Count(activity => activity.Dispatch is not null)}; numerical mode1 consumer not exercised");
    }

    private static byte[] Initialization()
    {
        byte[] payload = new byte[34];
        payload[1] = 189;
        for (int index = 0; index < 12; index++)
        {
            payload[2 + index] = (byte)(index * 17);
        }
        short[] directions = [short.MinValue, short.MaxValue, -1, 0, 4096, -4096, 385, -336, 43];
        for (int index = 0; index < directions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(14 + index * 2), directions[index]);
        }
        payload[32] = 170;
        payload[33] = 85;
        return payload;
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
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
