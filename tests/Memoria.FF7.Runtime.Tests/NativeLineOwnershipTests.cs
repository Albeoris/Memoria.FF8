using System.Diagnostics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLineOwnershipTests
{
    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_LINE_OWNERSHIP_SAVE") ?? throw new InvalidOperationException("Select a native LINE ownership fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        Ff7SessionSave save = CombinedSessionCodec.Read(stream).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), save);
        Ff7AutoPlayTarget target = new Ff7AutoPlayDiscovery(session).Observe().Events.Single(item => item.Id == save.AutoPlay!.Active!.EventId);
        int entry = session.Field.Scripts.Entities[target.Entity].ScriptOffsets[target.Script];
        bool admitted = false;
        session.Machine.InstructionExecuted += (entity, priority, pc, _) => admitted |= entity == target.Entity && priority == 1 && pc == entry;
        Check(Ff7AutoPlayNativeReadiness.HasCompletedSynchronousReturn(session.Machine.Capture(), session.Field.Scripts.Data.Span), "The source has no current owned synchronous return.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        while (controller.Running && controller.Steps < 100 && session.FieldGeneration == save.FieldGeneration && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            controller.Advance(1);
        }
        Check(controller.Error is null && admitted && session.MapId == 386 && session.FieldGeneration != save.FieldGeneration, $"Normal policy failed to drain setup and admit the original LINE: {controller.Error}; field={session.MapId}, callbacks={controller.Steps}");
        Check(policy.WorkProgress.EndsWith(":native-planning:0", StringComparison.Ordinal), "Finite native setup was replaced by a counterfactual graph search.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The original standalone changed.");
        Console.WriteLine($"PASS original native synchronous return, LINE admission and field departure: {controller.Steps} callbacks/{elapsed.Elapsed.TotalSeconds:F3}s; no native graph work; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
