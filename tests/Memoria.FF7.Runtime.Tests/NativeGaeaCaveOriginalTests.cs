using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF7.Events;

internal static class NativeGaeaCaveOriginalTests
{
    internal static void Inspect()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using FileStream input = File.OpenRead(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_CAVE_SAVE")!);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native cave source field={session.MapId}, triangle={player.Triangle}, mask={Convert.ToBase64String(session.Host.Process.BlockedTriangles)}, connections690={string.Join(',', session.ReadAutoPlayFieldConnections(690))}, connections691={string.Join(',', session.ReadAutoPlayFieldConnections(691))}, flags83={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83)}");
        Ff7AutoPlayState state = new() { DestinationField = 692 };
        var advice = Ff7AutoPlayPolicy.FindFieldEntryDestinationAdvice(state, observation, 692, player, session.Host.Process.BlockedTriangles, session.ReadAutoPlayFieldTopology, target => new Ff7AutoplayDriver(session).HasNativeGeometricContact(target));
        Console.WriteLine($"Native entry-qualified advice={advice.Target?.Id}, limited={advice.Limited}, lines690={session.ReadAutoPlayFieldTopology(690).Lines.Count}, lines691={session.ReadAutoPlayFieldTopology(691).Lines.Count}");
        foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.Gateway || target.ChangesField))
        {
            Console.WriteLine($"Native cave target {target.Id}, destination={target.DestinationField}, alternatives={string.Join(',', target.PossibleDestinationFields)}, complete={target.AnalysisComplete}, contact={new Ff7AutoplayDriver(session).HasNativeGeometricContact(target)}");
        }
    }

    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAEA_CAVE_SAVE") ?? throw new InvalidOperationException("Select a native cave entry fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The cave route unexpectedly requested a choice checkpoint."));
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 692, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        for (int index = 0; index < 64 && policy.State.Active is { Accepted: true } && controller.Running && controller.Error is null; index++)
        {
            controller.Advance(1);
        }
        policy.SetDestinationField(692);
        Stopwatch timer = Stopwatch.StartNew();
        List<string> entries = [];
        int map = session.MapId;
        string? first = null;
        while (timer.Elapsed.TotalSeconds < 120 && controller.Running && controller.Error is null && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle)
        {
            controller.Advance(1);
            first ??= policy.State.Active?.EventId;
            if (map != session.MapId)
            {
                Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                entries.Add($"{session.MapId}:{player.Triangle}");
                map = session.MapId;
            }
        }
        Ff7Actor actual = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Native cave planning: {controller.PlanningProgress}, pose={actual.Triangle}/{actual.X4096},{actual.Y4096},{actual.Z4096}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}");
        if (!controller.Completed && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle && Environment.GetEnvironmentVariable("MEMORIA_FF7_CAVE_DIAGNOSTIC_OUTPUT") is string diagnostic)
        {
            using FileStream output = File.Create(diagnostic);
            CombinedSessionCodec.Write(output, new() { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture() });
        }
        bool battle = session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle && session.Host.Process.GlobalRequestId is not null;
        Console.WriteLine($"Native cave entries: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, first={first}, entries={string.Join(',', entries)}, field={session.MapId}, battle={battle}, completed={controller.Completed}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || first != "gateway:1" || !battle && (!controller.Completed || session.MapId != 692 || !entries.SequenceEqual(["690:238", "692:20"])))
        {
            throw new InvalidOperationException("Native cave advice failed to preserve its entry-qualified return route.", controller.Failure);
        }
    }
}
