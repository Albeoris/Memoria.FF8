using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNightContinuationOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_NIGHT_SAVE") ?? throw new InvalidOperationException("Select a native night continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var original = policy.State.Active ?? throw new InvalidOperationException("The fixture has no returned source.");
        _ = policy.State.Choice ?? throw new InvalidOperationException("The fixture has no pending choice completion.");
        bool childReturned = false;
        bool descendingEntry = false;
        bool ladderCompleted = false;
        session.NativeTickCompleted += tick => childReturned |= tick.ScriptReturns.Any(returned => returned.Entity == 3 && returned.Priority == 6);
        session.NativeInteractionScriptRequested += admission => descendingEntry |= session.MapId == 636 && admission is { Entity: 5, Script: 4, Accepted: true, Started: true };
        session.NativeTickCompleted += tick => ladderCompleted |= tick.Field == "losinn" && session.Host.Process.Actors.Any(actor => actor.PlayerControlled && actor.Triangle == 16 && actor.Ladder is { Mode: 0, Completed: true, TargetTriangle: 16 });
        var current = session.Machine.ProvePersistentNavigationEntryHasNoEffect(11, 3);
        var ordinary = session.ReadNavigationEntryProof(11, 3, session.Machine.Input, allInputs: true);
        Console.WriteLine($"Current line no-effect: persistent={current.Complete}/{current.HasNoEffect}/{current.Reason}, ordinary={ordinary.Complete}/{ordinary.HasNoEffect}/{ordinary.Reason}");
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 10000 && controller.Steps < 8 && (policy.State.Choice is not null || policy.State.Active?.EventId == original.EventId) && timer.Elapsed.TotalSeconds < 10 && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        var vm = session.Machine.Capture();
        Console.WriteLine($"Night continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={vm.Tick}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}, choice={policy.State.Choice?.Key}, owner3={vm.Entities[3].Priority}/{vm.Entities[3].ProgramCounter:X4}, lines={string.Join(';', session.Host.Process.Lines.Where(line => line.Entity == 11).Select(line => $"{line.Across}/{line.Touch}"))}, status={policy.State.Status}, error={controller.Error}");
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
        bool consumed = coverage.Conditions?.HasExecutedEntry(original.ConditionFingerprint) == true;
        Console.WriteLine($"Night source completion: consumed={consumed}, choiceFinished={policy.State.Choice is null}, nativeChildReturned={childReturned}");
        if (!current.Complete || !current.HasNoEffect || !consumed || policy.State.Choice is not null || policy.State.PendingRestore is not null || policy.State.Active is null || policy.State.Active.EventId == original.EventId || childReturned || vm.Entities[3].Priority != 6 || vm.Entities[3].ProgramCounter != 0x07A9 || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The returned source and its actual choice did not finish while preserving the live presentation child and original fixture.");
        }
        policy.SetDestinationField(634);
        var destinationObservation = new Ff7AutoPlayDiscovery(session).Observe();
        Console.WriteLine($"Native departure offerings: {string.Join(';', destinationObservation.Events.Where(target => target.ChangesField || target.Kind == Ff7AutoPlayTargetKind.Gateway).Select(target => $"{target.Id}/{target.AnalysisComplete}/{target.DestinationField}/{string.Join(',', target.PossibleDestinationFields)}"))}");
        foreach (var target in destinationObservation.Events.Where(target => target.Entity is 5 or 8))
        {
            Console.WriteLine($"Local prerequisite: {target.Id}, complete={target.AnalysisComplete}, navigation={target.MayChangeNavigation}, ladders={target.LadderLandings.Length}, contact={new Ff7AutoplayDriver(session).HasNativeGeometricContact(target)}, reason={target.SupportReason}");
        }
        foreach (var target in destinationObservation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway))
        {
            var covered = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == destinationObservation.VisitId).Events.Single(item => item.Event.Id == target.Id);
            Console.WriteLine($"Gateway eligibility: contact={new Ff7AutoplayDriver(session).HasNativeGeometricContact(target)}, completed={covered.Completed}, consumed={covered.Conditions?.HasExecutedEntry(target.ConditionFingerprint)}, routes={covered.Routes.Count}");
        }
        for (int polls = 0; polls < 100000 && session.MapId != 634 && timer.Elapsed.TotalSeconds < 15 && controller.Error is null; polls++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Night destination continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, descended={descendingEntry}/{ladderCompleted}, map={session.MapId}, tick={session.Machine.Tick}, status={policy.State.Status}, error={controller.Error}");
        if (session.MapId != 634 || !descendingEntry || !ladderCompleted || controller.Error is not null || childReturned || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The completed night scene did not leave through its ordinary native destination route.");
        }
    }
}
