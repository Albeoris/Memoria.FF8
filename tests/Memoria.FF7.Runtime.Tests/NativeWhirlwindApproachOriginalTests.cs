using System.Diagnostics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeWhirlwindApproachOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using FileStream input = File.OpenRead(Environment.GetEnvironmentVariable("MEMORIA_FF7_WHIRLWIND_SAVE")!);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.MapId == 704, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 15 && controller.Running && controller.Error is null && policy.State.Active is not { Entity: >= 7 and <= 9, Script: 2, Accepted: true })
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native tutorial admission: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, active={policy.State.Active?.EventId}, accepted={policy.State.Active?.Accepted}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || policy.State.Active is not { Entity: >= 7 and <= 9, Script: 2, Accepted: true })
        {
            throw new InvalidOperationException("The native tutorial source was not admitted.", controller.Failure);
        }
    }
}
