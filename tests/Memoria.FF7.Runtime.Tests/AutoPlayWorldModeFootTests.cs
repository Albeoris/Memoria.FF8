using System.Collections;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldModeFootTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_FOOT_SAVE");
        string? targetId = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_MODE_TARGET");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(targetId))
        {
            Console.WriteLine("SKIP original post-exit goal: configure resources, standalone and native target identity.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidDataException("The configured post-exit fixture has no World allocation.");
        string[] parts = targetId.Split(':');
        Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(int.Parse(parts[1]), int.Parse(parts[2])).Scripts.Single(item => item.Id == targetId);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded post-exit fixture unexpectedly requested a choice."));
        Check(policy.State.DestinationField is not null && policy.State.NavigationGoal?.EventId == targetId, "The configured post-exit fixture has no retained requested entry.");
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoplayDriver driver = new(session);
        string native = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        Ff7AutoPlayWorldGuide beforeGuide = world.ReadAutoPlayNavigationGuide(target);
        Check(beforeGuide.Faces.Length > 0, "The original post-exit source has no connected native rider guide.");
        _ = policy.ChooseInput(driver);
        // Seed only the disposable cache left by the preceding completed action.
        // Native progress, control ownership and execution are the original fixture.
        Type candidateType = typeof(Ff7AutoPlayPolicy).GetNestedType("WorldModeCandidate", BindingFlags.NonPublic)!;
        Ff7AutoPlayWorldModeGoal goal = new(targetId, new(int.Parse(parts[1]), int.Parse(parts[2])), target.Script, world.ControlledModel);
        object candidate = Activator.CreateInstance(candidateType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [target, goal], null)!;
        IDictionary cache = (IDictionary)typeof(Ff7AutoPlayPolicy).GetField("worldModeCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(policy)!;
        cache.Add("mode:surface-rider:" + targetId, candidate);
        Check(policy.State.Active is not { Accepted: true }, "The cache fixture cannot interrupt an admitted native entry.");
        policy.State.Active = null;
        _ = policy.ChooseInput(driver);
        Check(cache.Count == 0 && policy.State.Active?.EventId == targetId, $"A completed vehicle cache survived rider ownership or displaced its native entry: cache={cache.Count}, active={policy.State.Active?.EventId}, goal={policy.State.NavigationGoal?.EventId}, status={policy.State.Status}.");
        Check(native == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }), "Cache retirement or goal planning changed native gameplay facts.");
        Ff7WorldEntity landed = world.CaptureAutoPlayMovementState().Player;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 64 && controller.Running && session.World is not null && session.Battle is null; step++)
        {
            controller.Advance(1);
            Check(session.World is null || world.ControlledModel == landed.Model, "The retained entry yielded to an unproductive transport reboard.");
        }
        Ff7WorldEntity advanced = world.CaptureAutoPlayMovementState().Player;
        Check(controller.Error is null && (advanced.X != landed.X || advanced.Z != landed.Z), $"The native rider entry did not advance after cache retirement: {controller.Error}");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Post-exit continuation changed its external standalone source.");
        Console.WriteLine($"PASS original post-exit cache retirement and retained rider goal after {controller.Steps} normal callbacks; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
