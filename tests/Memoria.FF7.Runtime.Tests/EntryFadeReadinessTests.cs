using System.Diagnostics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class EntryFadeReadinessTests
{
    internal static void Run()
    {
        Ff7FadeProcess entry = new(1, 224, 16, 0, 0, 0, false, EntryCurve: true);
        Check(Ff7AutoPlayPolicy.ShouldWaitForEntryFade(entry), "A finite native entry curve did not defer route planning.");
        Check(!Ff7AutoPlayPolicy.ShouldWaitForEntryFade(entry with { Speed = 0 }) && !Ff7AutoPlayPolicy.ShouldWaitForEntryFade(entry with { EntryCurve = false }) && !Ff7AutoPlayPolicy.ShouldWaitForEntryFade(entry with { Type = 7 }) && !Ff7AutoPlayPolicy.ShouldWaitForEntryFade(entry with { Type = 4 }) && !Ff7AutoPlayPolicy.ShouldWaitForEntryFade(null), "A persistent tint, non-entry fade or stationary effect acquired an indefinite readiness wait.");
        byte[] table = Enumerable.Range(0, 257).Select(value => (byte)Math.Min(value, 255)).ToArray();
        int frames = 0;
        while (Ff7AutoPlayPolicy.ShouldWaitForEntryFade(entry))
        {
            entry = Ff7NativeFade.Advance(entry, entryAlpha: table);
            frames++;
        }
        Check(frames == 14 && entry.Completed, "The readiness predicate did not end at the existing positive-speed native entry completion.");
        Console.WriteLine("PASS finite entry-fade readiness and persistent/non-entry effect exclusions");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ENTRY_FADE_SAVE") ?? throw new InvalidOperationException("Select a native entry-fade fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave saved = CombinedSessionCodec.Read(input);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), saved.Ff7!);
        Check(Ff7AutoPlayPolicy.ShouldWaitForEntryFade(session.Host.Process.FadeState), "The supplied original fixture is not at its finite entry fade.");
        Ff7FadeProcess initial = session.Host.Process.FadeState!;
        int bound = (initial.Position + initial.Speed - 1) / initial.Speed;
        long tick = session.Host.Process.Tick;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The entry readiness check reached an unexpected choice checkpoint."));
        int idle = policy.State.FrontierIdleSteps;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan previousClock = TimeSpan.Zero;
        while (Ff7AutoPlayPolicy.ShouldWaitForEntryFade(session.Host.Process.FadeState) && controller.Running && session.Host.Process.Tick - tick <= bound && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previousClock);
            previousClock = now;
            controller.Advance(1);
            Check(!controller.PlanningProgress.Pending && controller.PlanningProgress.PhysicalNodes == 0 && controller.PlanningProgress.DetachedNativeCallbacks == 0, "A held entry-fade frame started or blocked on physical/native route planning.");
        }
        Check(controller.Error is null && session.Host.Process.FadeState is { Completed: true } && session.Host.Process.Tick > tick && session.Host.Process.Tick - tick <= bound, $"Normal neutral native frames did not finish entry readiness: tick={session.Host.Process.Tick}, fade={session.Host.Process.FadeState}, error={controller.Error}.");
        Check(policy.State.FrontierIdleSteps == idle, "Finite entry readiness changed the controllable frontier supervisor history.");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The entry-fade continuation changed its original source.");
        Console.WriteLine($"PASS original App-clock entry fade; callbacks={controller.Steps}; ticks={tick}->{session.Host.Process.Tick}; fade-completed; zero planning before completion; source unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
