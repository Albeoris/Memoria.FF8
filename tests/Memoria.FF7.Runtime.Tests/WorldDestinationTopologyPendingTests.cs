using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class WorldDestinationTopologyPendingTests
{
    internal static void Run()
    {
        CheckRetainedTopology();
        CheckCompletedWithoutRoute();
        CheckDisposedTopology();
        Console.WriteLine("Requested world destinations preserve real cooperative topology work and reject completed unsupported routes.");
    }

    private static void CheckRetainedTopology()
    {
        int work = 0;
        bool disposed = false;
        Ff7AutoPlayWorldGuide transport = new([], 2, false, null);
        Ff7AutoPlayWorldModeGuide guide = new(transport, 0, null, null, transport, [], 2, false, null);
        IEnumerable<Ff7AutoPlayWorldModeGuide?> Search()
        {
            try
            {
                work++;
                yield return null;
                work++;
                yield return null;
                yield return guide;
            }
            finally
            {
                disposed = true;
            }
        }
        Ff7AutoPlayState state = new() { DestinationField = 10, NavigationGoal = new("world", "destination", "condition"), FrontierIdleSteps = 42, FrontierProgressKey = "same-frontier" };
        string before = JsonSerializer.Serialize(state);
        using Ff7WorldModeTopologyCursor cursor = new(Search().GetEnumerator());
        Check(!Ff7AutoPlayPolicy.HasPendingWorldModeTopology(null, null), "Missing topology work fabricated Pending.");

        // A later observation may start the job after the caller's first Pending check.
        bool pendingBeforeObservation = false;
        cursor.Resume(new(1, TimeSpan.MaxValue));
        bool pendingAfterObservation = Ff7AutoPlayPolicy.HasPendingWorldModeTopology(cursor.Result, cursor);
        Check(!pendingBeforeObservation && pendingAfterObservation && work == 1 && !disposed, "The second observation did not retain its real topology continuation.");
        Ff7AutoPlayPolicy.RequireWorldDestinationTargets([], pendingAfterObservation, null, state.DestinationField!.Value);
        cursor.Resume(new(1, TimeSpan.MaxValue));
        Check(Ff7AutoPlayPolicy.HasPendingWorldModeTopology(cursor.Result, cursor) && work == 2 && cursor.Operations == 2 && !disposed, "Another cooperative poll restarted or prematurely completed the retained topology.");
        Ff7AutoPlayPolicy.RequireWorldDestinationTargets([], true, "Unfinished route analysis", state.DestinationField.Value);
        Check(before == JsonSerializer.Serialize(state), "Topology Pending altered destination, goal, guards or frontier accounting.");
        cursor.Resume(new(1, TimeSpan.MaxValue));
        Check(cursor.Completed && ReferenceEquals(cursor.Result, guide) && disposed && !Ff7AutoPlayPolicy.HasPendingWorldModeTopology(cursor.Result, cursor), "A completed guide remained Pending.");
        Ff7AutoPlayPolicy.RequireWorldDestinationTargets(["mode:destination"], false, null, state.DestinationField.Value);
        Check(before == JsonSerializer.Serialize(state), "A ready topology result changed source planning metadata before normal activation.");
    }

    private static void CheckCompletedWithoutRoute()
    {
        IEnumerable<Ff7AutoPlayWorldModeGuide?> Empty()
        {
            yield return null;
        }
        using Ff7WorldModeTopologyCursor cursor = new(Empty().GetEnumerator());
        cursor.Resume(new(1, TimeSpan.MaxValue));
        Check(Ff7AutoPlayPolicy.HasPendingWorldModeTopology(null, cursor), "An unfinished search without a route was prematurely rejected.");
        cursor.Resume(new(1, TimeSpan.MaxValue));
        Check(cursor.Completed && cursor.Result is null && !Ff7AutoPlayPolicy.HasPendingWorldModeTopology(null, cursor), "Completed empty topology masqueraded as pending work.");
        ExpectUnsupported(() => Ff7AutoPlayPolicy.RequireWorldDestinationTargets([], false, null, 10), "A completed unsupported destination was silently retried.");
        ExpectUnsupported(() => Ff7AutoPlayPolicy.RequireWorldDestinationTargets([], false, "Unproved source", 10), "An incomplete source without a retained job fabricated Pending.");
    }

    private static void CheckDisposedTopology()
    {
        IEnumerable<Ff7AutoPlayWorldModeGuide?> Search()
        {
            yield return null;
            yield return null;
        }
        using Ff7WorldModeTopologyCursor cursor = new(Search().GetEnumerator());
        cursor.Resume(new(1, TimeSpan.MaxValue));
        cursor.Dispose();
        Check(!Ff7AutoPlayPolicy.HasPendingWorldModeTopology(null, cursor), "A disposed source/intent continuation fabricated resumable topology work.");
    }

    private static void ExpectUnsupported(Action action, string message)
    {
        try
        {
            action();
        }
        catch (NotSupportedException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
