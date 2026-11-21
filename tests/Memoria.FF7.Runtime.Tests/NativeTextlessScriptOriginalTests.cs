using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTextlessScriptOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        Ff7FieldResources textless = Ff7FieldResources.Load(installation, "nivl_4");
        if (textless.Scripts.TextOffset != textless.Scripts.Data.Length || textless.Scripts.Texts.Count != 0 || textless.Scripts.Entities.Count != 7 || textless.Scripts.ModelCount != 4)
        {
            throw new InvalidOperationException("The original textless script section lost its literal directory and empty text extent.");
        }
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TEXTLESS_CONNECTIVITY_SAVE") ?? throw new InvalidOperationException("Select a native field connectivity fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The connectivity fixture unexpectedly requested a choice checkpoint."));
        List<int> landings = [];
        session.Host.NativeOperationExecuted += (request, reply) =>
        {
            if (request.Command is FieldActorLadder ladder && reply.Completed && session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle == ladder.Triangle)
            {
                landings.Add(ladder.Triangle);
            }
        };
        policy.SetDestinationField(690);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 690, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        string? firstTarget = null;
        for (int poll = 0; poll < 4000 && timer.Elapsed.TotalSeconds < 60 && controller.Running && controller.Error is null && !landings.Contains(3); poll++)
        {
            controller.Advance(1);
            firstTarget ??= policy.State.Active?.EventId;
        }
        Ff7Actor actualPlayer = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"Original textless connectivity: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, world={session.World?.Resources.World}, first={firstTarget}, active={policy.State.Active?.EventId}/{policy.State.Active?.Accepted}/{policy.State.Active?.Returned}, triangle={actualPlayer.Triangle}, ladders={string.Join(',', landings)}, status={policy.Status}, completed={controller.Completed}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null && session.World is null && session.MapId == 689)
        {
            Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            Console.WriteLine($"Local source: navigation={observation.NavigationKey}, triangle={player.Triangle}, reachable={string.Join(',', Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle))}, gatewayOffered={observation.Events.Any(target => target.GatewayIndex == 0)}");
            foreach (Ff7AutoPlayTarget target in observation.Events.Where(target => target.Entity is 6 or 8 or 10 or 12 or 15 && target.Kind is Ff7AutoPlayTargetKind.LinePush or Ff7AutoPlayTargetKind.LineTouch))
            {
                bool route = Ff7AutoPlayPolicy.CanEstablishFieldGatewayComponentRoute(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Field.Triggers.Gateways[0], target, observation.Events, session.Host.Process.Lines);
                Console.WriteLine($"Local target: {target.Id}, complete={target.AnalysisComplete}, reason={target.SupportReason}, navigation={target.MayChangeNavigation}, field={target.ChangesField}, story={target.ChangesStory}, ladders={string.Join(';', target.LadderLandings.Select(landing => $"{landing.Entity}/{landing.Literal}/{landing.Command.Triangle}/{landing.Command.Mode}"))}, componentRoute={route}, recorded={Ff7AutoPlayPolicy.HasRecordedLocalNavigationStep(policy.State, observation, target)}, cf={target.ConditionFingerprint}");
            }
        }
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native connectivity fixture changed during its pure continuation check.");
        }
        if (controller.Error is not null || session.World is not null || session.MapId != 689 || firstTarget != "line:9:entity:15:script:2" || !landings.SequenceEqual([0, 3]))
        {
            throw new InvalidOperationException("The native local component route did not execute its first two real climb bridges.", controller.Failure);
        }
        if (controller.Completed)
        {
            throw new InvalidOperationException("A local climb prefix was incorrectly treated as destination completion.");
        }
    }
}
