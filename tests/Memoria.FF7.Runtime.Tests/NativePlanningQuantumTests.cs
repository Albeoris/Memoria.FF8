using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

internal static class NativePlanningQuantumTests
{
    internal static void Run()
    {
        Ff7NativePlanningQuantum ordinaryPhase = new(8, TimeSpan.Zero);
        Check(ordinaryPhase.TryBeginOperation() && !ordinaryPhase.TryBeginOperation(), "The elapsed ordinary allowance did not model a spent planning phase.");
        Ff7NativePlanningQuantum nativePhase = new(8, TimeSpan.FromHours(1));
        for (int operation = 0; operation < 8; operation++)
        {
            Check(nativePhase.TryBeginOperation(), "A new native phase inherited the spent ordinary phase allowance.");
        }
        Check(!nativePhase.TryBeginOperation() && ordinaryPhase.Operations == 1, "Native phase work escaped its own shared quantum or rewrote the ordinary budget.");
        List<int> visited = [];
        bool disposed = false;
        IEnumerable<AutoPlayPlanStep?> Search()
        {
            try
            {
                for (int candidate = 0; candidate < 6; candidate++)
                {
                    visited.Add(candidate);
                    yield return null;
                }
            }
            finally
            {
                disposed = true;
            }
        }
        using (Ff7NativePlanningCursor cursor = new(Search().GetEnumerator()))
        {
            cursor.Resume(new(2, TimeSpan.FromHours(1)));
            Check(visited.SequenceEqual([0, 1]) && !cursor.Completed && !disposed, "The first quantum lost its retained candidate position.");
            cursor.Resume(new(2, TimeSpan.FromHours(1)));
            Check(visited.SequenceEqual([0, 1, 2, 3]), "Candidate order changed across planning quanta.");
        }
        Check(disposed, "Cancelling a pending cursor did not dispose its retained search.");
        Ff7NativePlanningQuantum shared = new(3, TimeSpan.FromHours(1));
        Check(shared.TryBeginOperation(), "The parent did not consume its shared allowance.");
        visited.Clear();
        using Ff7NativePlanningCursor child = new(Search().GetEnumerator());
        child.Resume(shared);
        Check(shared.Operations == 3 && visited.Count == 2 && !shared.TryBeginOperation(), "A child obtained a fresh nested quantum.");
        while (!child.Completed)
        {
            child.Resume(new(1, TimeSpan.FromHours(1)));
        }
        Check(visited.SequenceEqual([0, 1, 2, 3, 4, 5]), "Quantum scheduling dropped or repeated candidates.");
        Ff7AutoPlayState state = new();
        AutoPlayEvent transport = new("transport", AutoPlayEventKind.Interaction, ConditionSignature: "source");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = new() { Event = transport, Unavailable = true, AvailabilityFingerprint = "old-negative" };
        state.Coverage.Visits.Add(new() { Visit = new("source", "region"), Events = [coverage] });
        AutoPlayEventAvailability admitted = new(false, [], Ff7AutoPlayPolicy.ReadNativeContinuationAvailability("stable-source"));
        Ff7AutoPlayPolicy.ApplyNativePlanningAvailability(state, "source", transport.Id, admitted);
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        planner.BeginEvent("source", transport.Id);
        planner.Completed("source", transport.Id, entrySignature: "source", entrySignatureAuthoritative: true, entryAvailabilityFingerprint: coverage.AvailabilityFingerprint, availabilityFingerprintAuthoritative: true);
        Check(coverage.CompletedAvailabilityFingerprints.Contains(admitted.Fingerprint!) && !coverage.CompletedAvailabilityFingerprints.Contains("old-negative"), "Resumed admission consumed the pre-proof availability instead of its stable capability.");
        Console.WriteLine("Native planning quantum checks passed.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
