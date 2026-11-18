using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeUpperFrontierTests
{
    public static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_UPPER_FRONTIER_SAVE") ?? throw new InvalidOperationException("An upper native frontier fixture is required.");
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream input = new(source);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayState state = session.AutoPlayState!;
        var read = Ff7AutoPlayPolicy.CreateRetainedNavigationAvailabilityReader(state);
        Console.WriteLine($"SOURCE component{observation.NavigationKey.Split(':').Last()}, tick{session.Host.Process.Tick}");
        foreach (var navigation in state.Coverage.NavigationVisits.Where(visit => visit.VisitKey == observation.VisitId))
        {
            var visit = state.Coverage.Visits.Single(visit => visit.Visit.Key == navigation.VisitKey);
            foreach (var native in navigation.Events)
            {
                var item = visit.Events.Single(item => item.Event.Id == native.Id);
                bool consumed = native.ConditionSignature is string condition && item.Conditions?.HasExecutedEntry(condition) == true;
                var guards = state.NavigationGuards.Values.Where(guard => guard.NavigationKey == navigation.Key && guard.EventId == native.Id).ToArray();
                if (!consumed || navigation.Key == observation.NavigationKey && guards.Any(guard => guard.MovementFailure is not null))
                {
                    var availability = read(navigation, native);
                    Console.WriteLine($"EVENT {navigation.Key.Split(':').Last()} {native.Id}, consumed{consumed}, unavailable{availability?.Unavailable}, failedRegions{guards.Count(guard => guard.MovementFailure is not null)}");
                }
            }
        }
        Ff7AutoPlayTarget[] pendingLines = observation.Events.Where(target => target.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave && state.NavigationGuards.Values.Any(guard => guard.EventId == target.Id && guard.MovementFailure is not null && guard.NavigationKey != observation.NavigationKey) && state.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id).Conditions?.HasExecutedEntry(target.ConditionFingerprint) != true).ToArray();
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A bounded frontier probe requested a choice checkpoint."));
        string nativeBefore = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        var method = typeof(Ff7AutoPlayPolicy).GetMethod("TryProveRemoteNativeNavigation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        object? plan = method.Invoke(policy, [observation with { Events = pendingLines }]);
        Console.WriteLine($"PENDING-LINE {string.Join(',', pendingLines.Select(target => target.Id))}: plan{plan}, result{policy.LastRemoteNativeProbe}");
        if (nativeBefore != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session))
        {
            throw new InvalidOperationException("A failed detached remote proof changed the live native source.");
        }
        if (!source.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("The original upper native frontier was changed.");
        }
    }
}
