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
        FieldVmSnapshot countdown = vm with { Entities = [owner with { WaitCounter = 90 }, child with { SyncStates = new FieldSyncState[8] }], PendingRequests = [] };
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", countdown, process, new byte[] { 0x24, 90, 0, 3, 1, 0x60 }, []), "An existing countdown could not reach its next synchronous native request.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", countdown, process, new byte[] { 0x24, 90, 0, 0xF8, 47, 0xF9 }, []), "A finite countdown depended on the identity of its following opcode.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWait(1, "source", countdown, process, new byte[] { 0, 90, 0, 0, 0, 0 }, []), "A stale counter without an executing WAIT authenticated finite work.");
        CheckCompletedSynchronousReturn();
        CheckMoviePreparation();
        CheckPlayerControlAcquisition();
        CheckOriginal();
    }

    private static void CheckPlayerControlAcquisition()
    {
        int[] saved = new int[8];
        saved[7] = 2;
        FieldEntitySnapshot child = new(0, 6, 0, saved, new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray(), ModelBound: true);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "admitted-player-child", 1, true, new byte[1280], new byte[256], [child]);
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, EntityBound = true, PlayerControlled = true }] };
        byte[] script = [0x33, 1, 0];
        string before = JsonSerializer.Serialize(new { vm, process });
        Check(Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process, script), "An admitted current player child could not acquire native control before speculation.");
        Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process, [0x33, 0, 0]), "An unlock opcode was mistaken for pending control acquisition.");
        Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process, [0x4A, 1, 0]), "An unrelated control opcode authenticated player acquisition.");
        Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process, [0x33]), "A truncated control opcode authenticated acquisition.");
        foreach (FieldEntitySnapshot wrong in new[] { child with { Priority = 7 }, child with { SavedProgramCounters = new int[8] }, child with { ModelBound = false }, child with { ProgramCounter = 2 }, child with { SavedProgramCounters = [0, 0, 0, 0, 0, 0, 0, 3] } })
        {
            Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm with { Entities = [wrong] }, process, script), "An unadmitted, unrelated or invalid native continuation authenticated acquisition.");
        }
        Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process with { Actors = [process.Actors[0] with { Entity = 1 }] }, script), "A different native player owner inherited acquisition.");
        Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process with { Actors = [] }, script), "An absent controlled actor authenticated acquisition.");
        Check(!Ff7AutoPlayNativeReadiness.HasPendingPlayerControlAcquisition(vm, process with { PlayerLocked = true }, script), "Already-owned control required another acquisition wait.");
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Control readiness changed native state.");
    }

    private static void CheckMoviePreparation()
    {
        FieldEntitySnapshot owner = new(0, 6, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldHostRequest request = new(1, 0, 6, 0, new FieldMovieCommand(FieldMovieOperation.Prepare, 17));
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "prepared-movie", 1, true, new byte[1280], new byte[256], [owner], PendingRequests: [request]);
        Ff7FieldProcess process = new() { Movie = new(17, false, 0, false, null) };
        byte[] script = [0xF8, 17];
        string before = JsonSerializer.Serialize(new { vm, process });
        Check(Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process, script), "A completed current native preparation could not return on its next poll.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process with { GlobalCommand = Ff7GlobalCommand.Movie }, script), "A busy native global command authenticated preparation.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process with { Movie = process.Movie! with { Playing = true } }, script), "An active movie authenticated a preparation return.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process with { Movie = null }, script), "A missing prepared resource authenticated return.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process with { Movie = process.Movie! with { PreparedId = 18 } }, script), "A different prepared resource authenticated return.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process, [0xF8, 18]), "Different native operands authenticated return.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process, [0xF9, 17]), "A different current opcode authenticated preparation.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm, process, [0xF8]), "A truncated current opcode authenticated preparation.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm with { PendingRequests = [] }, process, script), "An unowned prepared movie authenticated return.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm with { PendingRequests = [request with { Entity = 1 }] }, process, script), "A missing request owner authenticated preparation.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm with { PendingRequests = [request with { Priority = 5 }] }, process, script), "A different current priority authenticated preparation.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm with { PendingRequests = [request with { ProgramCounter = 1 }] }, process, script), "A stale request PC authenticated preparation.");
        Check(!Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(vm with { PendingRequests = [request with { Command = new FieldMovieCommand(FieldMovieOperation.Play) }] }, process, script), "Pending playback inherited preparation completion.");
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Preparation readiness changed native state.");
    }

    private static void CheckCompletedSynchronousReturn()
    {
        FieldEntitySnapshot owner = new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldSyncState[] sync = new FieldSyncState[8];
        sync[6] = FieldSyncState.Done;
        int[] owners = Enumerable.Repeat(-1, 8).ToArray();
        owners[6] = 0;
        FieldEntitySnapshot child = owner with { ProgramCounter = 3, SyncStates = sync, SyncOwners = owners, PartyCharacter = 2 };
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "owned-return", 1, true, new byte[1280], new byte[256], [owner, child]);
        byte[] script = [3, 1, 0xC1, 0];
        string before = JsonSerializer.Serialize(vm);
        Check(Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm, script), "A current owned completed request did not yield its native continuation.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm, [2, 1, 0xC1, 0]), "An asynchronous request inherited Done ownership.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm, [3, 1, 0xA1, 0]), "A different priority inherited Done ownership.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm, [3, 0, 0xC1, 0]), "A different target inherited Done ownership.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm, [3, 1]), "A truncated request authenticated completion.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm with { Entities = [owner with { ProgramCounter = 3 }, child] }, script), "A stale Done record authenticated a caller at RET.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm with { Entities = [owner, child with { SyncOwners = Enumerable.Repeat(-1, 8).ToArray() }] }, script), "An unrelated owner authenticated completion.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(vm with { Entities = [owner, child with { SyncStates = new FieldSyncState[8] }] }, script), "A non-completed request authenticated a return.");
        Check(before == JsonSerializer.Serialize(vm), "Return inspection changed native facts.");
        FieldVariables variables = new(vm.PersistentVariables, vm.MapVariables);
        variables.WriteByte(FieldBank.Persistent1Byte, 9, 2);
        FieldVmSnapshot party = vm with { PersistentVariables = variables.CopyPersistent() };
        Check(Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(party, [6, 0, 0xC1, 0]), "A current native party request did not resolve its owned return.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(party, [5, 0, 0xC1, 0]), "An asynchronous party request authenticated a return.");
        Check(!Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(party, [6, 3, 0xC1, 0]), "An invalid party slot authenticated a return.");
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
