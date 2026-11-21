using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeFieldTransferArrivalOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_FIELD_TRANSFER_SAVE") ?? throw new InvalidOperationException("Select a field-transfer fixture.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during field transfer."));
        policy.SetDestinationField(70);
        bool forwardOwned = false;
        bool reverseOwned = false;
        bool retargeted = false;
        bool consumedEquivalent = false;
        long sourceGeneration = session.FieldGeneration;
        session.NativeTickCompleted += observation =>
        {
            if (observation.Transition is not null)
            {
                bool actualOwner = observation.TransitionOwnership is { RootEntity: 6, RootScript: 2, OwnerEntity: 6, OwnerPriority: 1 } && session.AutoPlayState!.Active is { Entity: 6, Script: 2, Accepted: true, TransitionOwned: true } active && active.Generation == observation.Generation;
                forwardOwned |= observation.Generation == sourceGeneration && observation.Transition.Map == 70 && actualOwner;
                reverseOwned |= observation.Generation == sourceGeneration + 1 && observation.Transition.Map == 74 && actualOwner;
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 20 && controller.Steps < 200 && controller.Error is null && !consumedEquivalent)
        {
            controller.Advance(1);
            if (reverseOwned && !retargeted && session.MapId == 74 && session.AutoPlayState!.Active is not { Accepted: true })
            {
                policy.SetDestinationField(70);
                retargeted = true;
            }
            if (retargeted && session.MapId == 70)
            {
                Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
                var visit = session.AutoPlayState!.Coverage.Visits.Single(item => item.Visit.Key == observation.VisitId);
                var duplicate = visit.Events.Single(item => item.Event.Id == "line:0:entity:6:script:1");
                consumedEquivalent = Ff7AutoPlayPolicy.HasConsumedEffectAlias(session.AutoPlayState, observation.VisitId, duplicate.Event) && !duplicate.Completed;
            }
        }
        Console.WriteLine($"Native field-transfer aliases: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, forwardOwned={forwardOwned}, reverseOwned={reverseOwned}, consumedEquivalent={consumedEquivalent}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !forwardOwned || !reverseOwned || !consumedEquivalent || !SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Equivalent native door entries did not retain their real admission and transfer ownership.", controller.Failure);
        }
    }
}
