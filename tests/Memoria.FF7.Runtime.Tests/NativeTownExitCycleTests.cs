using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTownExitCycleTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TOWN_EXIT_SAVE") ?? throw new InvalidOperationException("Select an external town exit fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        string visitKey = save.AutoPlay!.NavigationGoal!.NavigationKey.Split(":nav:")[0];
        string targetId = save.AutoPlay.NavigationGoal.EventId!;
        int EntryCount(Ff7AutoPlayState state) => state.Coverage.Visits.Single(visit => visit.Visit.Key == visitKey).Events.Single(item => item.Event.Id == targetId).Conditions?.ConsumedEntries.Count ?? 0;
        int originalEntries = EntryCount(save.AutoPlay);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The exit continuation reached an unsupported choice checkpoint."));
        // Mirror only the existing explicit App start's observational watchdog window.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        int transitions = 0;
        int terminalAdmissions = 0;
        int transportAdmissions = 0;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission.Accepted && admission.Started)
            {
                if (session.MapId == 514 && admission.Entity == 1 && admission.Script == 1)
                {
                    terminalAdmissions++;
                }
                if (session.MapId == 518 && admission.Entity == 11 && admission.Script == 2)
                {
                    transportAdmissions++;
                }
            }
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        bool inspected = false;
        int previous = session.MapId;
        double proofMilliseconds = 0;
        double firstDiscoveryMilliseconds = 0;
        double retainedDiscoveryMilliseconds = 0;
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int step = 0; step < 1200 && controller.Running && elapsed.Elapsed < TimeSpan.FromSeconds(15); step++)
        {
            controller.Advance(1);
            if (!inspected && session.MapId == 514 && session.Machine.Tick >= 3)
            {
                string nativeBefore = JsonSerializer.Serialize(session.CaptureAutoPlayNavigation());
                Stopwatch timer = Stopwatch.StartNew();
                FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(1, 1, session.Machine.Input, allInputs: true);
                proofMilliseconds = timer.Elapsed.TotalMilliseconds;
                Ff7AutoPlayDiscovery discovery = new(session);
                timer.Restart();
                Ff7AutoPlayObservation observation = discovery.Observe();
                firstDiscoveryMilliseconds = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
                _ = discovery.Observe();
                retainedDiscoveryMilliseconds = timer.Elapsed.TotalMilliseconds;
                Check(proof is { Complete: true, HasNoEffect: true }, "The immutable native guard did not close every input path before its physical query.");
                Check(observation.Events.All(target => target.Entity != 1 || target.Script != 1), "The certified terminal town leaf remained a planning frontier.");
                Check(nativeBefore == JsonSerializer.Serialize(session.CaptureAutoPlayNavigation()), "Town entry proof or discovery changed actual native state, RNG, input or hosts.");
                inspected = true;
            }
            if (previous != session.MapId)
            {
                transitions++;
            }
            previous = session.MapId;
            if (session.World is not null || session.Host.HasUnfinishedBattle || policy.State.PendingRestore is not null)
            {
                break;
            }
        }
        Check(inspected && controller.Error is null && session.World is not null, $"The normal town route did not reach native world allocation: {controller.Error}; status={policy.Status}; callbacks={controller.Steps}.");
        Check(transitions >= 2 && transportAdmissions > 0, "Town exit bypassed its actual native transport admissions.");
        Check(terminalAdmissions == 0 && EntryCount(policy.State) == originalEntries, "Terminal town exploration synthesized admission or entry credit.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Town exit continuation changed its authoritative source.");
        Console.WriteLine($"PASS original town terminal-effect exclusion and normal native world exit; callbacks={controller.Steps}; field-transitions={transitions}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; proof={proofMilliseconds:F3}ms; first-discovery={firstDiscoveryMilliseconds:F3}ms; retained-discovery={retainedDiscoveryMilliseconds:F3}ms; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
