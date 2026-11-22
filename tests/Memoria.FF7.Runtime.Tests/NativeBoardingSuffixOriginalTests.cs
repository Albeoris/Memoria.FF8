using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBoardingSuffixOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_BOARDING_SUFFIX_SAVE") ?? throw new InvalidOperationException("Select a native boarding approach fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during the native boarding approach."));
        Ff7WorldRuntime world = session.World!;
        Ff7WorldEntity initial = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
        FieldInfo certificate = typeof(Ff7AutoPlayPolicy).GetField("destinationBoardingSuffix", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo searches = typeof(Ff7AutoPlayPolicy).GetField("destinationBoardingSearches", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string? completed = null;
        int pending = 0;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            if (completed is not null && ((IDictionary)searches.GetValue(policy)!).Count != 0)
            {
                throw new InvalidOperationException("A normal footstep rebuilt the already proved boarding suffix.");
            }
            completed ??= (string?)certificate.GetValue(policy);
            pending += completed is null && driver.PlanningPending ? 1 : 0;
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && controller.Steps < 8 && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
        }
        Ff7WorldEntity actual = world.Process.Entities.Single(entity => entity.Model == world.ControlledModel);
        Console.WriteLine($"Native boarding suffix reuse: initialPolls={pending}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={world.Process.Tick}, position=({initial.X},{initial.Z})→({actual.X},{actual.Z}), status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || completed is null || pending <= Ff7AutoPlayProgressWatchdog.IdleStepLimit || controller.Steps != 8 || (actual.X, actual.Z) == (initial.X, initial.Z) || policy.State.DestinationField != 353 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Completed boarding advice was not retained across ordinary native approach movement.", controller.Failure);
        }
    }
}
