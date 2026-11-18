using Memoria.AutoPlay;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using System.Text.Json;

internal static class NativePlanningQuantumTests
{
    internal static void Run()
    {
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

    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_PLANNING_SAVE") ?? throw new InvalidOperationException("An external planning fixture is required.");
        byte[] original = File.ReadAllBytes(path);
        using MemoryStream input = new(original);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A planning-only check requested a choice checkpoint."));
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        for (int readiness = 0; readiness < 32 && !policy.NativePlanningPending && controller.Running; readiness++)
        {
            controller.Advance(1);
        }
        Check(policy.NativePlanningPending, "The fixture did not reach a native planning boundary: " + controller.Error + "; " + policy.Status);
        string before = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        double maxResumeMilliseconds = 0;
        for (int slice = 0; slice < 16; slice++)
        {
            driver.BeginPlanningStep();
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            policy.ChooseInput(driver);
            maxResumeMilliseconds = Math.Max(maxResumeMilliseconds, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            Check(driver.PlanningPending && policy.NativePlanningPending, "The native search did not yield cooperatively.");
            Check(before == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session), "A pending search changed its live native source.");
        }
        long generation = policy.NativePlanningGeneration;
        policy.State.DestinationField = 1;
        driver.BeginPlanningStep();
        policy.ChooseInput(driver);
        Check(policy.NativePlanningGeneration > generation, "An intent change resumed a stale proof cursor.");
        generation = policy.NativePlanningGeneration;
        session.SetInput(0x1000, 0x1000);
        driver.BeginPlanningStep();
        policy.ChooseInput(driver);
        Check(policy.NativePlanningGeneration > generation, "A native source change resumed a stale proof cursor.");
        generation = policy.NativePlanningGeneration;
        policy.State.PendingRestore = new("unused-planning-cancellation-fixture");
        driver.BeginPlanningStep();
        policy.ChooseInput(driver);
        Check(!policy.NativePlanningPending && policy.NativePlanningGeneration == generation && policy.State.NativeRoute is null, "A pending restore resumed or installed an old native proof.");
        policy.State.PendingRestore = null;
        driver.BeginPlanningStep();
        policy.ChooseInput(driver);
        Check(policy.NativePlanningPending, "Clearing a cancelled intent did not start a fresh ephemeral search.");
        Ff7SessionSave saved = session.Capture();
        Check(!JsonSerializer.Serialize(saved).Contains("nativePlanning", StringComparison.Ordinal), "An ephemeral planning job entered the save contract.");
        generation = policy.NativePlanningGeneration;
        policy.State.NativeRoute = new("external-route", "external-condition", session.FieldGeneration, [new(Ff7AutoplayDriver.ReadNativeRouteFingerprint(session), default)], "unreached-terminal");
        driver.BeginPlanningStep();
        try
        {
            policy.ChooseInput(driver);
        }
        catch (NotSupportedException)
        {
            // Existing route admission rejects its unrelated absent owner.
        }
        Check(!policy.NativePlanningPending && policy.NativePlanningGeneration == generation, "An externally installed route was overwritten by an old proof cursor.");
        policy.Dispose();
        Check(!policy.NativePlanningPending, "Disposal retained a detached native planning branch.");
        using Ff7Session cold = Ff7Session.Restore(installation, saved);
        using Ff7AutoPlayPolicy coldPolicy = new(cold, (_, _) => throw new InvalidOperationException("A cold planning-only check requested a choice checkpoint."));
        Check(!coldPolicy.NativePlanningPending, "A cold save reconstructed an ephemeral search cursor.");
        Check(original.SequenceEqual(File.ReadAllBytes(path)), "Planning checks changed the external fixture.");
        Console.WriteLine($"Native planning source/intent/restore cancellation, pending immutability, disposal and cold restart passed; 16 pending ChooseInput resumptions max {maxResumeMilliseconds:F3} ms. Atomic callback/setup/hash work is not preempted.");
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
