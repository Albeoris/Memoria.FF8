using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineOwnershipTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_OWNERSHIP_SAVE") ?? throw new InvalidOperationException("Select a native LINE ownership fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        Ff7AutoPlayTarget target = new Ff7AutoPlayDiscovery(session).Observe().Events.Single(item => item.Id == save.AutoPlay!.Active!.EventId);
        int entry = session.Field.Scripts.Entities[target.Entity].ScriptOffsets[target.Script];
        bool admitted = false;
        session.Machine.InstructionExecuted += (entity, priority, pc, _) => admitted |= entity == target.Entity && priority == 1 && pc == entry;
        Check(Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(session.Machine.Capture(), session.Field.Scripts.Data.Span), "The source has no current owned synchronous return.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        while (controller.Running && controller.Steps < 100 && session.FieldGeneration == save.FieldGeneration && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && admitted && session.MapId == 386 && session.FieldGeneration != save.FieldGeneration, $"Normal policy failed to drain setup and admit the original LINE: {controller.Error}; field={session.MapId}, callbacks={controller.Steps}");
        Check(policy.WorkProgress.EndsWith(":native-planning:0", StringComparison.Ordinal), "Finite native setup was replaced by a counterfactual graph search.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The original standalone changed.");
        Console.WriteLine($"PASS original native synchronous return, LINE admission and field departure: {controller.Steps} callbacks/{elapsed.Elapsed.TotalSeconds:F3}s; no native graph work; source unchanged.");
    }

    internal static void RunMovieOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_OWNERSHIP_SAVE") ?? throw new InvalidOperationException("Select a native movie preparation fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        FieldHostRequest prepare = save.Vm.PendingRequests!.Single(item => item.Command is FieldMovieCommand { Operation: FieldMovieOperation.Prepare });
        Check(Ff7AutoPlayNativeReadiness.HasReadyMoviePreparation(save.Vm, session.Host.Process, session.Field.Scripts.Data.Span), "The current preparation has no proved completion.");
        bool playAdmitted = false;
        session.Host.NativeOperationExecuted += (request, _) => playAdmitted |= request.Entity == prepare.Entity && request.Priority == prepare.Priority && request.Command is FieldMovieCommand { Operation: FieldMovieOperation.Play };
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        while (controller.Running && controller.Steps < 8 && session.Host.Process.Movie is not { Playing: true } && elapsed.Elapsed < TimeSpan.FromSeconds(3))
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && playAdmitted && session.Host.Process is { GlobalCommand: Ff7GlobalCommand.Movie, Movie: { Playing: true, RequestId: not null } }, $"Normal policy did not reach the actual movie boundary: {controller.Error}");
        Check(!(session.Machine.Capture().PendingRequests ?? []).Any(request => request.Id == prepare.Id), "Native preparation did not consume its own request.");
        Check(policy.WorkProgress.EndsWith(":native-planning:0", StringComparison.Ordinal), "Completed native preparation was replaced by a counterfactual graph.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The original movie fixture changed.");
        Console.WriteLine($"PASS original normal-policy preparation to native movie handoff: {controller.Steps} callbacks/{elapsed.Elapsed.TotalSeconds:F3}s; zero native graph work; source unchanged. External movie playback/LINE departure not simulated.");
    }

    internal static void RunComponentOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_OWNERSHIP_SAVE") ?? throw new InvalidOperationException("Select a native component-transition fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), saved);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget target = observation.Events.Single(target => target.Id == "line:1:entity:6:script:2");
        Ff7AutoPlayActiveEvent stale = saved.AutoPlay!.Active!;
        int entry = session.Field.Scripts.Entities[target.Entity].ScriptOffsets[target.Script];
        bool admitted = false;
        bool returned = false;
        bool jumpCompleted = false;
        bool childReturned = false;
        int childEntity = -1;
        int childPriority = -1;
        FieldActorJump? jump = null;
        session.Machine.InstructionExecuted += (entity, priority, pc, _) => admitted |= entity == target.Entity && priority == 1 && pc == entry;
        session.Machine.ScriptDispatched += dispatch =>
        {
            if (admitted && dispatch.SourceEntity == target.Entity && dispatch.SourcePriority == 1 && dispatch.Accepted)
            {
                childEntity = dispatch.TargetEntity;
                childPriority = dispatch.TargetPriority;
            }
        };
        session.Machine.ScriptReturned += receipt =>
        {
            returned |= admitted && receipt.Entity == target.Entity && receipt.Priority == 1;
            childReturned |= jump is not null && receipt.Entity == childEntity && receipt.Priority == childPriority;
        };
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldActorJump command && request.Entity == childEntity && request.Priority == childPriority)
            {
                jump = command;
                jumpCompleted |= reply.Completed;
            }
        };
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch watch = Stopwatch.StartNew();
        while (controller.Running && controller.Steps < 512 && !(returned && childReturned && jumpCompleted) && watch.Elapsed < TimeSpan.FromSeconds(8))
        {
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(controller.Error is null && admitted && returned && childReturned && jumpCompleted && jump is not null && player.Triangle == jump.Triangle, $"Normal policy failed to replan and finish the actual native child: {controller.Error}; callbacks={controller.Steps}, jump={jump}, childReturned={childReturned}");
        Check(!policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == stale.VisitId).Events.Single(item => item.Event.Id == stale.EventId).Conditions!.HasExecutedEntry(stale.ConditionFingerprint), "Retiring a stale planned component fabricated execution of its lower LINE.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The component fixture changed.");
        Console.WriteLine($"PASS original normal-policy stale component replan, actual upper LINE admission, owned JUMP completion/child RET and lower-component arrival: {controller.Steps} callbacks/{watch.Elapsed.TotalSeconds:F3}s; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
