using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeGatewayRouteOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GATEWAY_ROUTE_SAVE") ?? throw new InvalidOperationException("Select a native gateway-route fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (key, _) => "memory:" + key);
        HashSet<(int, int, int)> poses = [];
        session.NativeTickCompleted += _ =>
        {
            var actor = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            poses.Add((actor.Triangle, actor.X4096, actor.Y4096));
        };
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int poll = 0; poll < 10000 && timer.Elapsed.TotalSeconds < 20 && controller.Steps < 120 && session.MapId == 654 && controller.Error is null; poll++)
        {
            controller.Advance(1);
        }
        var player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        var work = controller.PlanningProgress;
        Console.WriteLine($"Gateway route: callbacks={controller.Steps}, tick={session.Machine.Tick}, field={session.MapId}, seconds={timer.Elapsed.TotalSeconds:F3}, poses={poses.Count}, actor={player.Triangle}:{player.X4096}:{player.Y4096}:{player.Direction}, physical={work.PhysicalNodes}, detached={work.DetachedNativeCallbacks}, graph={work.NativeGraphExpanded}, status={policy.State.Status}, error={controller.Error}");
        if (session.MapId != 655 || controller.Error is not null || poses.Count < 2 || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The current native gateway route did not reach its selected field or changed the original fixture.");
        }
    }
}
