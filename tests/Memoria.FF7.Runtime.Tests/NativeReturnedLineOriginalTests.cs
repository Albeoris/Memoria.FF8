using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeReturnedLineOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_RETURNED_LINE_SAVE") ?? throw new InvalidOperationException("Select a returned native activation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the next native scene admission."));
        Ff7AutoPlayActiveEvent original = policy.State.Active!;
        Ff7AutoPlayTarget offered = new Ff7AutoPlayDiscovery(session).Observe().Events.Single(target => target.Id == original.EventId);
        HashSet<int> returned = [];
        session.NativeTickCompleted += tick =>
        {
            foreach (FieldScriptReturn owner in tick.ScriptReturns.Where(owner => owner.Priority == 6 && owner.Entity is 21 or 23 or 25))
            {
                returned.Add(owner.Entity);
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int callback = 0; callback < 8 && policy.State.Active is { Accepted: true }; callback++)
        {
            controller.Advance(1);
        }
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
        if (controller.Error is not null || policy.State.Active is not null || coverage.Conditions?.HasExecutedEntry(original.ConditionFingerprint) != true || offered.ConditionFingerprint != original.ConditionFingerprint && coverage.Conditions.HasExecutedEntry(offered.ConditionFingerprint))
        {
            throw new InvalidOperationException("The actual returned root did not retire under its original source condition before the planning handoff.", controller.Failure);
        }
        policy.SetDestinationField(456);
        Ff7Actor initialPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Line goal = session.Host.Process.Lines.Single(line => line.Entity == 9);
        int Distance(Ff7Actor actor) => Ff7Session.DistanceToSegment(new(goal.X1, goal.Y1, goal.Z1), new(goal.X2, goal.Y2, goal.Z2), new(actor.X4096 >> 12, actor.Y4096 >> 12, actor.Z4096 >> 12), out _);
        float ApproachDistance(Ff7Actor actor) => System.Numerics.Vector2.DistanceSquared(new(actor.X4096 / 4096f, actor.Y4096 / 4096f), new((goal.X1 + goal.X2) / 2f, (goal.Y1 + goal.Y2) / 2f));
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && timer.Elapsed.TotalSeconds < 10 && (session.Machine.Variables.ReadByte(FieldBank.MapByte, 13) == 0 || returned.Count != 3))
        {
            controller.Advance(1);
        }
        int released = session.Machine.Variables.ReadByte(FieldBank.MapByte, 13);
        Ff7Actor finalPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        bool healthyApproach = policy.State.Active is { Entity: 9, Script: 2, Accepted: false } && finalPlayer.Triangle != initialPlayer.Triangle && ApproachDistance(finalPlayer) < ApproachDistance(initialPlayer);
        Console.WriteLine($"Native returned LINE: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Machine.Tick}, oldRootRetired={coverage.Conditions.HasExecutedEntry(original.ConditionFingerprint)}, map13={released}, actualNpcReturns={string.Join(',', returned.Order())}, active={policy.State.Active?.EventId}, accepted={policy.State.Active?.Accepted}, field={session.MapId}, initial=({initialPlayer.X4096},{initialPlayer.Y4096},{initialPlayer.Triangle}), final=({finalPlayer.X4096},{finalPlayer.Y4096},{finalPlayer.Triangle}), distanceSquared={Distance(initialPlayer)}→{Distance(finalPlayer)}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || (released == 0 || returned.Count != 3) && !healthyApproach || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The subsequent real scene did not release the preserved native polling workers.", controller.Failure);
        }
    }
}
