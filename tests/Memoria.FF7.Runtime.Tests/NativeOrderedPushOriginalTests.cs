using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeOrderedPushOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ORDERED_PUSH_SAVE") ?? throw new InvalidOperationException("Select an ordered native prerequisite fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the ordered native jump."));
        Ff7AutoPlayActiveEvent original = policy.State.Active!;
        string History() => JsonSerializer.Serialize(policy.State.Coverage.Visits.Select(visit => new { visit.Visit.Key, Events = visit.Events.Select(item => new { item.Event.Id, item.Completed, item.Conditions?.ConsumedEntries, item.Routes }) }));
        string history = History();
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget current = observation.Events.Single(target => target.Id == original.EventId);
        MethodInfo reorder = typeof(Ff7AutoPlayPolicy).GetMethod("TryReorderUnacceptedNavigationPrerequisite", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        foreach (Ff7AutoPlayActiveEvent protectedOwner in new[] { original with { Accepted = true }, original with { Generation = original.Generation + 1 }, original with { NavigationPrerequisiteGoal = null } })
        {
            policy.State.Active = protectedOwner;
            if ((bool)reorder.Invoke(policy, [observation, current])! || policy.State.Active != protectedOwner || history != History() || native != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
            {
                throw new InvalidOperationException("Prerequisite ordering replaced accepted, foreign-generation or unauthenticated native planning ownership.");
            }
        }
        policy.State.Active = original;
        bool jumped = false;
        bool returned = false;
        session.Host.NativeOperationExecuted += (request, _) => jumped |= request is { Entity: 28, Priority: 6, Command: FieldActorJump { X: -840, Y: -53, Triangle: 150, Duration: 17 } };
        session.NativeTickCompleted += tick => returned |= tick.ScriptReturns.Any(owner => owner.Entity == 17 && owner.Priority == 1);
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            if (policy.State.Active?.Entity != 17 && (action.Horizontal != 0 || action.Vertical != 0 || action.Held != 0 || action.Talk))
            {
                throw new InvalidOperationException("Movement toward a later prerequisite preceded the first current-route source.");
            }
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int callback = 0; callback < 8 && controller.Error is null && policy.State.Active?.Entity != 17; callback++)
        {
            controller.Advance(1);
        }
        if (controller.Error is not null || original is not { Entity: 9, Accepted: false } || policy.State.Active is not { Entity: 17, Script: 2, Accepted: false } || history != History() || policy.State.NavigationGoal != original.NavigationPrerequisiteGoal)
        {
            throw new InvalidOperationException($"The restored unaccepted later prerequisite was not reordered before movement while preserving original history and route intent: old={original.Entity}/{original.Accepted}/provenance={original.ExecutionProvenance is not null}, active={policy.State.Active?.Entity}/{policy.State.Active?.Accepted}, historySame={history == History()}, intentSame={policy.State.NavigationGoal == original.NavigationPrerequisiteGoal}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}.", controller.Failure);
        }
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && timer.Elapsed.TotalSeconds < 20 && !(jumped && returned && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled))
        {
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native ordered Push: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, jumped={jumped}, rootReturned={returned}, triangle={player.Triangle}, active={policy.State.Active?.EventId}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !jumped || !returned || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || player.Triangle != 150 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The first ordered prerequisite did not execute its genuine native jump and control return.", controller.Failure);
        }
    }
}
