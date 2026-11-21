using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTutorialOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_REPEAT_TUTORIAL_SAVE") ?? throw new InvalidOperationException("Select a native repeated tutorial fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var original = policy.State.Choice ?? throw new InvalidOperationException("The fixture has no active repeated question.");
        HashSet<int> returned = [];
        bool exitSelected = false;
        bool prematureRestore = false;
        session.NativeTickCompleted += tick =>
        {
            foreach (var owner in tick.ScriptReturns.Where(owner => owner.Entity is 8 or 25 && owner.Priority == 6 || owner.Entity == 10 && owner.Priority == 1))
            {
                returned.Add(owner.Entity);
            }
        };
        int? Choose(Ff7Dialogue dialogue)
        {
            int? selection = policy.ChooseDialogueSelection(dialogue);
            exitSelected |= dialogue is { Window: 2, Text: 30, FirstChoice: 1, LastChoice: 2 } && selection == 2;
            prematureRestore |= policy.State.PendingRestore is not null;
            return selection;
        }
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: Choose, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && timer.Elapsed.TotalSeconds < 10 && controller.Steps < 300 && (session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || !returned.Contains(10)))
        {
            controller.Advance(1);
        }
        var coverage = policy.State.Coverage.Choices.Single(item => item.Key == original.Key);
        for (int poll = 0; poll < 16 && !coverage.CompletedOptions.Contains(2) && policy.State.PendingRestore is null && controller.Error is null; poll++)
        {
            controller.Advance(1);
        }
        int answer = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x10);
        Console.WriteLine($"Tutorial continuation: tick={session.Machine.Tick}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, selectedExit={exitSelected}, answer={answer}, returned={string.Join(',', returned.Order())}, locked={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, completed={string.Join(',', coverage.CompletedOptions)}, deferred={string.Join(',', coverage.PolicyDeferredOptions)}, parentRestore={policy.State.PendingRestore is not null}, status={policy.State.Status}, root={policy.State.Active?.Returned}/{policy.State.Active?.ExecutionProvenance?.Owners.Length}, error={controller.Error}");
        if (!exitSelected || prematureRestore || answer != 2 || !new[] { 8, 25, 10 }.All(returned.Contains) || controller.Error is not null || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || !coverage.CompletedOptions.Contains(2) || coverage.CompletedOptions.Contains(1) || !coverage.PolicyDeferredOptions.Contains(1) || original.NativeSourceOwned != false || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The normal tutorial continuation did not execute its actual exit and native caller returns unchanged.");
        }
    }
}
