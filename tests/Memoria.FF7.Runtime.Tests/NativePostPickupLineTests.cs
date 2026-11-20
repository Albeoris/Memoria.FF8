using System.Diagnostics;
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
