using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeNpcAnimationLoopOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NPC_ANIMATION_LOOP_SAVE") ?? throw new InvalidOperationException("Select a native NPC animation-loop fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        var original = policy.State.Active!;
        var before = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        bool childReturned = false;
        session.NativeTickCompleted += tick => childReturned |= tick.ScriptReturns.Any(owner => owner.Entity == 15 && owner.Priority == 6);
        var proof = session.Machine.AnalyzeScriptAtProgramCounter(15, session.Machine.Capture().Entities[15].ProgramCounter);
        Console.WriteLine($"NPC animation cycle analysis: complete={proof.Complete}, reads={proof.Reads.Length}/{proof.HostReads.Length}, exits={proof.ExitProgramCounters.Length}, navigation={proof.MayChangeNavigation}, reason={proof.Reason}");
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int poll = 0; poll < 1000 && controller.Error is null && timer.Elapsed.TotalSeconds < 10 && controller.Steps < 8; poll++)
        {
            controller.Advance(1);
        }
        var vm = session.Machine.Capture();
        var actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        bool consumed = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId).Conditions?.HasExecutedEntry(original.ConditionFingerprint) == true;
        bool moved = actor.X4096 != before.X4096 || actor.Y4096 != before.Y4096 || actor.Z4096 != before.Z4096;
        Console.WriteLine($"NPC presentation continuation: callbacks={controller.Steps}, tick={vm.Tick}, sourceConsumed={consumed}, childReturned={childReturned}, moved={moved}, next={policy.State.Active?.EventId}, status={policy.State.Status}, error={controller.Error}");
        if (!consumed || childReturned || !moved || vm.Entities[15].Priority != 6 || policy.State.Active?.EventId == original.EventId || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || controller.Error is not null || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The returned source did not release navigation while preserving its native NPC presentation loop.");
        }
        policy.SetDestinationField(651);
        timer.Restart();
        for (int poll = 0; poll < 10000 && controller.Error is null && timer.Elapsed.TotalSeconds < 30 && session.MapId == 653; poll++)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"NPC loop destination follow-through: field={session.MapId}, tick={session.Machine.Tick}, callbacks={controller.Steps}, status={policy.State.Status}, error={controller.Error}");
        if (session.MapId != 651 || controller.Error is not null || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("An independent NPC presentation loop blocked the adjacent native destination.");
        }
    }
}
