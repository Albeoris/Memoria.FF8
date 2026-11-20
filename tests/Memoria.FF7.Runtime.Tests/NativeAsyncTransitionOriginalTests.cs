using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAsyncTransitionOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ASYNC_TRANSITION_SAVE") ?? throw new InvalidOperationException("Select a native asynchronous transition fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        CombinedSessionSave saved = CombinedSessionCodec.Read(stream);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), saved.Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        Ff7NativeTransitionOwnership? proof = null;
        FieldHostRequest? request = null;
        int polls = 0;
        while (controller.Running && polls++ < 512 && elapsed.Elapsed.TotalSeconds < 15)
        {
            controller.Advance(10);
            FieldVmSnapshot vm = session.Machine.Capture();
            request = (vm.PendingRequests ?? []).FirstOrDefault(item => item.Command is FieldTransitionCommand && policy.State.Active?.ExecutionProvenance?.Owners.Any(owner => owner.Entity == item.Entity && owner.Priority == item.Priority && owner.ParentEntity >= 0) == true);
            if (request is not null && policy.State.Active is { Accepted: true } active)
            {
                proof = Ff7Session.ReadTransitionOwnership(vm, request, active, session.FieldGeneration, []);
                if (proof is not null)
                {
                    break;
                }
            }
        }
        Console.WriteLine($"Original asynchronous transition source: callbacks={controller.Steps}, field={session.Field.Name}, tick={session.Machine.Tick}, movie={session.Host.Process.Movie}, active={policy.State.Active?.EventId}, proof={proof}, request={request}, owners={string.Join(';', policy.State.Active?.ExecutionProvenance?.Owners.Select(owner => $"{owner.Entity}/{owner.Priority}/script{owner.Script}/parent{owner.ParentEntity}/{owner.ParentPriority}") ?? [])}, physical={controller.PlanningProgress.PhysicalNodes}, detached={controller.PlanningProgress.DetachedNativeCallbacks}, error={controller.Error}");
        if (controller.Error is not null || proof is null || policy.State.Active is not { Accepted: true, TransitionOwned: false } || request is null || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("Normal native execution did not preserve its actual asynchronous transition ancestry without granting pending completion.");
        }
        Ff7SessionSave checkpoint = session.Capture();
        string nativeBefore = System.Text.Json.JsonSerializer.Serialize(checkpoint with { AutoPlay = null, EventCoverage = null });
        using MemoryStream roundtrip = new();
        CombinedSessionCodec.Write(roundtrip, saved with { Ff7 = checkpoint });
        roundtrip.Position = 0;
        using Ff7Session restored = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(roundtrip).Ff7!);
        FieldVmSnapshot restoredVm = restored.Machine.Capture();
        Ff7NativeTransitionOwnership? restoredProof = Ff7Session.ReadTransitionOwnership(restoredVm, request, restored.AutoPlayState?.Active, restored.FieldGeneration, []);
        if (restoredProof != proof || nativeBefore != System.Text.Json.JsonSerializer.Serialize(restored.Capture() with { AutoPlay = null, EventCoverage = null }) || restored.AutoPlayState?.Active?.TransitionOwned != false)
        {
            throw new InvalidOperationException("The standalone roundtrip changed native facts or lost its recorded asynchronous source proof.");
        }
        if (Ff7Session.ReadTransitionOwnership(restoredVm, request, restored.AutoPlayState!.Active! with { ExecutionProvenance = null }, restored.FieldGeneration, []) is not null)
        {
            throw new InvalidOperationException("A missing historical provenance marker invented asynchronous ancestry.");
        }
        Console.WriteLine("PASS standalone in-memory provenance roundtrip; native state, progress and pending credit unchanged; missing historical provenance remains conservative");
    }
}
