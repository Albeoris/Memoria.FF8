using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePassiveInputOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PASSIVE_INPUT_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A returned native source requires no new choice anchor."));
        Ff7AutoPlayActiveEvent original = policy.State.Active!;
        Ff7AutoPlayExecutionOwner owner = original.ExecutionProvenance!.Owners.Single();
        if (!session.Machine.CanPollCurrentScriptWithoutInput(owner.Entity, owner.Priority))
        {
            throw new InvalidOperationException("The original child is not in an authenticated current unmet-input poll.");
        }
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (policy.State.Active is not null && controller.Error is null && controller.Steps < 8 && timer.Elapsed.TotalSeconds < 5)
        {
            controller.Advance(1);
        }
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(entry => entry.Event.Id == original.EventId);
        if (controller.Error is not null || policy.State.Active is not null || coverage.Conditions?.HasExecutedEntry(original.ConditionFingerprint) != true || session.Machine.Capture().Entities[owner.Entity].Priority != owner.Priority || session.Progress.Gil != source.Progress.Gil)
        {
            throw new InvalidOperationException("The returned parent did not hand control back while preserving its live material input worker.", controller.Failure);
        }
        policy.SetDestinationField(428);
        if (policy.State.DestinationField != 428 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Destination admission or original source purity failed after parent completion.");
        }
        controller.Advance(1);
        Console.WriteLine($"Destination first callback: status={policy.State.Status}, active={policy.State.Active?.EventId}, tick={session.Machine.Tick}, error={controller.Error}.");
        Console.WriteLine($"Native passive input handoff: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Machine.Tick}, parentCompleted=true, liveWorkerPriority={owner.Priority}, destination={policy.State.DestinationField}, gil={session.Progress.Gil}.");
    }
}
