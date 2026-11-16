using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldFieldGoalTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original world and field resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_FIELD_GOAL_SAVE") ?? throw new InvalidOperationException("Select an external world-to-field travel goal.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        var expected = source.AutoPlay?.NavigationGoal ?? throw new InvalidOperationException("The fixture has no recorded exploration goal.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), source);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the recorded travel goal."));
        // Isolate route execution from the supplied attempt's already exhausted observational deadline.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        bool completed = false;
        for (int step = 0; step < 256 && controller.Running && session.Battle is null && !completed; step++)
        {
            controller.Advance(1);
            var visit = policy.State.Coverage.NavigationVisits.Single(item => item.Key == expected.NavigationKey);
            var target = policy.State.Coverage.Visits.Single(item => item.Visit.Key == visit.VisitKey).Events.Single(item => item.Event.Id == expected.EventId);
            completed = expected.Conditions is not null && target.Conditions?.ConsumedEntries.Contains(expected.Conditions) == true;
        }
        if (controller.Error is not null || !completed || !bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException($"World and field did not execute their recorded common-condition goal: {controller.Error ?? policy.State.Status}; steps={controller.Steps}, completed={completed}.");
        }
        Console.WriteLine($"PASS original world-to-field goal, actual source-condition consumption and no route oscillation after {controller.Steps} normal steps; source unchanged");
    }
}
