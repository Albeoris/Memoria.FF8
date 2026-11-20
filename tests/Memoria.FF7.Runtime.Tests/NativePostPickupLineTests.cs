using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePostPickupLineTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_POST_PICKUP_SAVE") ?? throw new InvalidOperationException("Select an external post-pickup fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        Ff7AutoPlayActiveEvent original = save.AutoPlay?.Active ?? throw new InvalidOperationException("The fixture has no native line entry owner.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("This continuation must not create choice files."));
        // Mirror only the existing explicit App start's observational watchdog window.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        bool admitted = false;
        bool returned = false;
        int materia = session.Progress.Materia.Length;
        session.NativeInteractionScriptRequested += admission => admitted |= admission.Entity == original.Entity && admission.Script == original.Script && admission.Accepted && admission.Started;
        session.NativeTickCompleted += observation => returned |= admitted && observation.ScriptReturns.Any(completed => completed.Entity == original.Entity && completed.Priority == 1);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        for (int i = 0; i < 2000 && controller.Running && elapsed.Elapsed < TimeSpan.FromSeconds(15); i++)
        {
            controller.Advance(1);
            if (session.Host.HasUnfinishedBattle || session.FieldGeneration != save.FieldGeneration)
            {
                break;
            }
        }
        Check(controller.Error is null && admitted && returned, $"The native latched entry did not genuinely admit and return: {controller.Error}.");
        Check(session.Host.HasUnfinishedBattle || session.FieldGeneration != save.FieldGeneration, "The normal controller did not reach its next native battle or field boundary.");
        Check(policy.State.Active?.EventId != original.EventId, "The completed native entry still owns the next navigation action.");
        Check(session.Progress.Materia.Length == materia, "Returning from the completed pickup changed materia ownership.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Post-pickup continuation changed its authoritative source.");
        Console.WriteLine($"PASS original post-pickup real LINE admission/RET and next native boundary; callbacks={controller.Steps}; field={session.Field.Name}; tick={session.Machine.Tick}; battle={session.Host.HasUnfinishedBattle}; elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged");
    }

    internal static void RunCycleOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_POST_PICKUP_SAVE") ?? throw new InvalidOperationException("Select an external post-pickup fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources are required.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave save = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        string nativeBefore = JsonSerializer.Serialize(session.CaptureAutoPlayNavigation());
        Stopwatch proofTime = Stopwatch.StartNew();
        FieldNavigationEntryProof proof = session.ReadNavigationEntryProof(2, 5, session.Machine.Input);
        proofTime.Stop();
        FieldScriptEntryDependencies dependencies = session.Machine.AnalyzeEntryDependencies(2, 5);
        Console.WriteLine($"terminal proof={proof.Complete}/{proof.HasNoEffect}: {proof.Reason}; incoming={string.Join(';', dependencies.Reads)}");
        Check(proof.Complete && proof.HasNoEffect, "The completed pickup still exposes a native effect for another heading or an observable scratch publication.");
        Ff7AutoPlayDiscovery discovery = new(session);
        Stopwatch observationTime = Stopwatch.StartNew();
        Ff7AutoPlayObservation observation = discovery.Observe();
        observationTime.Stop();
        Stopwatch retainedTime = Stopwatch.StartNew();
        _ = discovery.Observe();
        retainedTime.Stop();
        Console.WriteLine($"current proof={proofTime.Elapsed.TotalMilliseconds:F3}ms first-discovery={observationTime.Elapsed.TotalMilliseconds:F3}ms retained-discovery={retainedTime.Elapsed.TotalMilliseconds:F3}ms");
        Check(observation.Events.All(target => target.Entity != 2 || target.Script != 5), "The certified terminal native entry remains an exploration frontier.");
        Check(nativeBefore == JsonSerializer.Serialize(session.CaptureAutoPlayNavigation()), "Terminal proof or discovery changed actual native state, RNG, input or host state.");
        Ff7AutoPlayActiveEvent original = save.AutoPlay?.Active ?? throw new InvalidOperationException("The fixture lost its stale pickup target.");
        int Entries(Ff7AutoPlayState state) => state.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId).Conditions?.ConsumedEntries.Count ?? 0;
        int entries = Entries(save.AutoPlay!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("This terminal continuation must not create choice files."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        for (int callback = 0; callback < 4 && controller.Running && !session.Host.HasUnfinishedBattle; callback++)
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && policy.State.Active?.EventId != original.EventId, "The native policy retained the certified terminal pickup as its active goal.");
        Check(Entries(policy.State) == entries, "Terminal planning invented native pickup entry consumption.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Terminal entry analysis changed its source.");
        Console.WriteLine("PASS original post-pickup terminal guard, dead query publication and derived discovery exclusion; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
