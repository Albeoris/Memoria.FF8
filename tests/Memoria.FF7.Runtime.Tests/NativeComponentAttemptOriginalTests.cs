using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeComponentAttemptOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COMPONENT_ATTEMPT_SAVE") ?? throw new InvalidOperationException("Select a native component-priority fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice before the native floor bridge."));
        bool selected = false;
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            if (session.MapId == 355 && policy.State.Active is { Accepted: false } active)
            {
                selected |= active is { Entity: 5, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch };
                if (!selected)
                {
                    throw new InvalidOperationException($"The reachable upper-floor story frontier lost its recorded local bridge to {active.EventId}, goal={policy.State.NavigationGoal}.");
                }
            }
            return action;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (controller.Error is null && !selected && timer.Elapsed.TotalSeconds < 20)
        {
            controller.Advance(1);
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native component priority: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, tick={session.Machine.Tick}, triangle={player.Triangle}, active={policy.State.Active?.EventId}, goal={policy.State.NavigationGoal?.EventId}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || !selected || policy.State.NavigationGoal?.EventId != "actor:29:script:1" || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("A lower-component physical attempt still demoted the independently reachable upper-floor story frontier.", controller.Failure);
        }
    }
}
