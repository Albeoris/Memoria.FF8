using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayReadinessTests
{
    public static void Run()
    {
        CheckFiniteRequest();
        CheckStagnation();
        CheckTimedMessageClose();
        CheckOriginal();
    }

    private static void CheckFiniteRequest()
    {
        FieldVmSnapshot vm = Snapshot();
        Ff7FieldProcess process = new();
        byte[] script = [3, 1, 0x40, 0];
        string before = JsonSerializer.Serialize(vm);
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", vm, process, script, []), "An existing synchronous native request could not finish before frontier exhaustion.");
        FieldEntitySnapshot[] done = vm.Entities.ToArray();
        done[1] = done[1] with { Priority = 7, SyncStates = States(FieldSyncState.Done) };
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", vm with { Entities = done }, process, script, []), "A completed native child could not resume its still-waiting caller.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", vm, process, new byte[4], []), "A stale saved sync owner was mistaken for current finite native work.");
        FieldScriptInputDemand input = new("wait", vm.ProgramIdentity, 1, 2, 3, FieldScriptInputKind.Pressed, 0x20, true, true);
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", vm, process, script, [input]), "A required native button wait was treated as autonomous finite work.");
        FieldEntitySnapshot[] preempted = vm.Entities.ToArray();
        preempted[1] = preempted[1] with { Priority = 1 };
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", vm with { Entities = preempted }, process, script, []), "An unrelated preempting request inherited a suspended child's readiness.");
        FieldEntitySnapshot[] idle = vm.Entities.ToArray();
        idle[1] = idle[1] with { SyncStates = States(FieldSyncState.None) };
        FieldVmSnapshot empty = vm with { Entities = idle, PendingRequests = [new(1, 1, 7, 3, new FieldAnimation(0, 1, true, true))] };
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", empty, process, script, []), "An ambient standalone animation masked a stable empty frontier.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "component", empty, process, script, [input with { Priority = 7 }]), "An ambient button poller masked a stable empty frontier.");
        Check(before == JsonSerializer.Serialize(vm), "Native readiness inspection changed scheduler facts.");
    }

    private static void CheckStagnation()
    {
        FieldVmSnapshot vm = Snapshot();
        Ff7AutoPlayNativeReadiness readiness = new();
        bool waiting = true;
        for (int cycle = 0; cycle <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; cycle++)
        {
            waiting = readiness.ShouldWait(1, "component", vm with { Tick = cycle, NextRequestId = cycle + 1 }, new(), new byte[] { 3, 1, 0x40, 0 }, []);
        }
        Check(!waiting, "Tick/request-ID changes kept an infinite native wait alive indefinitely.");
        Check(readiness.ShouldWait(2, "component", vm, new(), new byte[] { 3, 1, 0x40, 0 }, []), "A new native allocation inherited an unrelated exhaustion budget.");
        readiness.Reset();
        Check(readiness.ShouldWait(1, "component", vm, new(), new byte[] { 3, 1, 0x40, 0 }, []), "A real intervening frontier did not reset the readiness observation scope.");
        Ff7AutoPlayNativeReadiness moving = new();
        for (int cycle = 0; cycle < Ff7AutoPlayNativeReadiness.RepeatedObservationLimit + 2; cycle++)
        {
            Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0, X4096 = cycle, AnimationFrame16 = cycle }] };
            Check(moving.ShouldWait(1, "component", vm, process, new byte[] { 3, 1, 0x40, 0 }, []), "Observable owned native movement was confused with a stalled request.");
        }
    }

    private static void CheckTimedMessageClose()
    {
        FieldVmSnapshot vm = Snapshot();
        FieldEntitySnapshot timer = new(6, 6, 145, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        vm = vm with { Entities = [.. vm.Entities, timer], PendingRequests = [new(1, 1, 2, 3, new FieldDialogueCommand(0, 198))] };
        Ff7FieldProcess process = new() { Dialogues = [new(1, 0, 198, null, null, 0, AwaitingInput: true)] };
        byte[] script = [3, 1, 0x40, 0, 0, 0, 0x24, 145, 0, 0x54, 0, 0];
        Ff7AutoPlayNativeReadiness readiness = new();
        for (ushort remaining = 145; remaining > 0; remaining--)
        {
            FieldEntitySnapshot[] entities = vm.Entities.ToArray();
            entities[2] = timer with { WaitCounter = remaining };
            Check(readiness.ShouldWait(1, "timed-message", vm with { Entities = entities }, process, script, []), "The authored message-close countdown was mistaken for a stalled synchronous message.");
        }
        bool waiting = true;
        Ff7AutoPlayNativeReadiness stalled = new();
        for (int cycle = 0; cycle <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; cycle++)
        {
            waiting = stalled.ShouldWait(1, "stalled-timer", vm, process, script, []);
        }
        Check(!waiting, "An unchanged message-close timer bypassed the stagnation bound.");
        Ff7AutoPlayNativeReadiness ambient = new();
        waiting = true;
        for (int cycle = 0; cycle < 256 && waiting; cycle++)
        {
            FieldEntitySnapshot[] entities = vm.Entities.ToArray();
            entities[2] = timer with { WaitCounter = (ushort)(10 - cycle % 10) };
            waiting = ambient.ShouldWait(1, "ambient-loop", vm with { Entities = entities }, process, script, []);
        }
        Check(!waiting, "A repeating ambient countdown masked an exhausted frontier indefinitely.");
        Ff7AutoPlayNativeReadiness closing = new();
        for (int height = 145; height > 0; height--)
        {
            Ff7Dialogue dialogue = process.Dialogues[0] with { Presentation = new(7, 147, height) };
            Check(closing.ShouldWait(1, "closing-message", vm with { Entities = vm.Entities[..2] }, process with { Dialogues = [dialogue] }, script, []), "Native owned window-closing progress was omitted from readiness.");
        }
    }

    private static FieldVmSnapshot Snapshot()
    {
        FieldEntitySnapshot owner = new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        int[] owners = Enumerable.Repeat(-1, 8).ToArray();
        owners[2] = 0;
        FieldEntitySnapshot child = new(3, 2, 0, new int[8], States(FieldSyncState.Waiting), owners);
        return new(1, FieldMachine.SemanticProfile, "native-readiness", 1, true, new byte[1280], new byte[256], [owner, child]);
    }

    private static FieldSyncState[] States(FieldSyncState state)
    {
        FieldSyncState[] states = new FieldSyncState[8];
        states[2] = state;
        return states;
    }

    private static void CheckOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_READINESS_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP native frontier readiness: configure resources and an external standalone setup fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayObservation original = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayNativeReadiness readiness = new();
        Check(readiness.ShouldWait(session.FieldGeneration, original.NavigationKey, session.Machine.Capture(), session.Host.Process, session.Field.Scripts.Data.Span, session.Machine.ReadCurrentInputDemands()), "Original native setup was declared exhausted before its owned continuation.");
        Check(before == JsonSerializer.Serialize(session.Capture()), "Readiness inspection changed original native state.");
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, envelope with { Ff7 = session.Capture() });
        serialized.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(serialized).Ff7!);
        Check(before == JsonSerializer.Serialize(cold.Capture()), "Cold restoration changed the pending native setup facts.");
        using Ff7AutoPlayPolicy policy = new(cold, (_, _) => throw new InvalidOperationException("Neutral setup requested a choice checkpoint."));
        Ff7AutoplayDriver driver = new(cold);
        long generation = cold.FieldGeneration;
        int map = cold.MapId;
        short story = cold.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0);
        int callbacks = 0;
        while (!new Ff7AutoPlayDiscovery(cold).Observe().Events.Any(target => target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave) && callbacks < Ff7AutoPlayNativeReadiness.ObservationLimit)
        {
            Ff7AutoplayInput input = policy.ChooseInput(driver);
            Ff7Actor player = cold.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            if (player.Ladder is { Completed: false })
            {
                Ff7AutoplayInput nativeLadder = driver.Steer(new Vector3(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f));
                Check(input == nativeLadder && input.Horizontal == 0 && input.Vertical == 0 && !input.Talk, $"Existing native ladder setup received an unrelated physical input: {input}.");
            }
            else
            {
                Check(input.Horizontal == 0 && input.Vertical == 0 && input.Held == 0 && !input.Talk, $"Existing native setup required an invented movement or ambient input pulse: {input}.");
            }
            driver.Apply(input);
            cold.AdvanceTick(advanceClock: false);
            callbacks++;
            Check(cold.MapId == map && cold.FieldGeneration == generation && cold.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) == story, "Native setup crossed a story or field boundary before native contact availability.");
        }
        Check(callbacks > 0 && callbacks < Ff7AutoPlayNativeReadiness.ObservationLimit, "The original finite setup did not naturally publish its native exit contact.");
        _ = policy.ChooseInput(driver);
        Check(policy.State.Active is not null, "Normal planning did not reconsider the newly published native contact.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Neutral fixture continuation changed its external source.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
