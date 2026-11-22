using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeDeferredPlanningOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_DEFERRED_PLANNING_SAVE") ?? throw new InvalidOperationException("Select a deferred native planning fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native boarding approach."));
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The fixture requires its native world continuation.");
        Ff7WorldEntity initial = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
        var historicalEvents = policy.State.Coverage.Visits.SelectMany(visit => visit.Events).ToArray();
        string History() => JsonSerializer.Serialize(historicalEvents.Select(item => new { item.Event.Id, item.Conditions?.ConsumedEntries, item.Routes }));
        string history = History();
        int pendingPolls = 0;
        bool movement = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            pendingPolls += driver.PlanningPending ? 1 : 0;
            movement |= !driver.PlanningPending && (action.Horizontal != 0 || action.Vertical != 0 || action.Held != 0);
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && !movement && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        Ff7WorldEntity actual = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
        Console.WriteLine($"Native deferred planning: polls={pendingPolls}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={world.Process.Tick}, movement={movement}, position=({initial.X},{initial.Z})→({actual.X},{actual.Z}), status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || pendingPolls <= Ff7AutoPlayProgressWatchdog.IdleStepLimit || !movement || (actual.X, actual.Z) == (initial.X, initial.Z) || policy.State.DestinationField != 353 || history != History() || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Deferred planning consumed the native idle-step allowance or failed to publish its genuine first boarding-approach movement.", controller.Failure);
        }
    }
}
