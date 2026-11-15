using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePreemptedReadinessTests
{
    public static void Run()
    {
        FieldEntitySnapshot owner = new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldSyncState[] sync = new FieldSyncState[8];
        sync[3] = FieldSyncState.Waiting;
        int[] owners = Enumerable.Repeat(-1, 8).ToArray();
        owners[3] = 0;
        FieldEntitySnapshot child = new(3, 2, 0, new int[8], sync, owners);
        FieldHostRequest move = new(12, 1, 2, 3, new FieldActorMove(100, 200));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "preempted-native-work", 1, true, new byte[1280], new byte[256], [owner, child], PendingRequests: [move]);
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0 }], Motions = [new(12, 1, 100, 200, true, true)] };
        byte[] script = [3, 1, 0x60, 0];
        string before = JsonSerializer.Serialize(new { vm, process });
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", vm, process, script, []), "Finite higher-priority movement was mistaken for an exhausted synchronous frontier.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", vm with { PendingRequests = [move with { Priority = 1 }] }, process, script, []), "A different request priority authenticated the preemptor.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", vm with { PendingRequests = [move with { ProgramCounter = 2 }] }, process, script, []), "A different request PC authenticated the preemptor.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", vm, process with { Motions = [] }, script, []), "A missing movement owner authenticated an unproved preemptor.");
        Ff7FieldProcess arrived = process with { Motions = [], Actors = [process.Actors[0] with { X4096 = 100 << 12, Y4096 = 200 << 12 }] };
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", vm, arrived, script, []), "An arrived move could not publish its pending completion on the next native callback.");
        FieldScriptInputDemand input = new("button", vm.ProgramIdentity, 1, 2, 3, FieldScriptInputKind.Pressed, 0x20, true, true);
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", vm, process, script, [input]), "A required physical input was treated as autonomous preempting work.");
        Ff7AutoPlayNativeReadiness stalled = new();
        bool waiting = true;
        for (int repeat = 0; repeat <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; repeat++)
        {
            waiting = stalled.ShouldWait(1, "source", vm with { Tick = repeat }, process, script, []);
        }
        Check(!waiting, "A stationary preemptor masked an exhausted frontier indefinitely.");
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Readiness inspection changed native execution facts.");
        FieldVmSnapshot countdown = vm with { Entities = [owner with { WaitCounter = 90 }, child with { SyncStates = new FieldSyncState[8] }] };
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", countdown, process, new byte[] { 0x24, 90, 0, 3, 1, 0x60 }, []), "An existing countdown could not reach its next synchronous native request.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", countdown, process, new byte[] { 0x24, 90, 0, 0, 0, 0 }, []), "An unrelated ambient wait authenticated future synchronous work.");
        CheckOriginal();
    }

    private static void CheckOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PREEMPTED_READINESS_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original preempted readiness: configure resources and an external standalone continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        FieldHostRequest move = saved.Vm.PendingRequests!.Single(request => request.Command is FieldActorMove);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native handoff."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        bool finished = false;
        bool nextFrontier = false;
        int callbacks = 0;
        for (; callbacks < 2048 && controller.Running; callbacks++)
        {
            controller.Advance(1);
            finished |= !(session.Machine.Capture().PendingRequests ?? []).Any(request => request.Id == move.Id);
            nextFrontier = session.Host.PendingMinigame is not null || session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null);
            if (nextFrontier)
            {
                break;
            }
        }
        Check(finished && nextFrontier, $"Normal autoplay failed before its next native decision or minigame handoff: {controller.Error}");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The normal-policy regression changed its standalone source.");
        Console.WriteLine($"PASS original preemptor, countdown and native decision handoff after {callbacks + 1} callbacks");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
