using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCraterBridgeOriginalTests
{
    internal static void RunSceneAdmission()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        using FileStream input = File.OpenRead(Environment.GetEnvironmentVariable("MEMORIA_FF7_CRATER_BRIDGE_SAVE")!);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice."));
        policy.SetDestinationField(69);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.MapId == 69, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 15 && controller.Running && controller.Error is null && policy.State.Active is not { Entity: 11, Script: 2, Accepted: true })
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native scene goal: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, field={session.MapId}, active={policy.State.Active?.EventId}, accepted={policy.State.Active?.Accepted}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (controller.Error is not null || policy.State.Active is not { Entity: 11, Script: 2, Accepted: true })
        {
            throw new InvalidOperationException("The ordinary scene destination did not admit its native line.", controller.Failure);
        }
    }

    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CRATER_BRIDGE_SAVE") ?? throw new InvalidOperationException("Select a native component-bridge fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        CombinedSessionSave combined = CombinedSessionCodec.Read(input);
        using Ff7Session session = Ff7Session.Restore(installation, combined.Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The native bridge route has no choice checkpoint."));
        if (policy.State.DestinationField != 709)
        {
            policy.SetDestinationField(709);
        }
        Ff7AutoPlayObservation observed = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        foreach (Ff7AutoPlayTarget target in observed.Events.Where(target => target.Script == 2 && target.Entity is 4 or 5 or 9))
        {
            int? length = Ff7AutoPlayPolicy.ReadFieldGatewayComponentRouteLength(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Field.Triggers.Gateways[1], target, observed.Events, session.Host.Process.Lines);
            Console.WriteLine($"Native bridge advice: entity={target.Entity}, complete={target.AnalysisComplete}, jumps={target.PossibleJumpLandings.Length}, bridges={length}");
        }
        bool forwardJump = false;
        bool reverseJump = false;
        session.Host.NativeOperationExecuted += (request, _) =>
        {
            forwardJump |= request is { Entity: 14, Priority: 6, ProgramCounter: 0x9EE, Command: FieldActorJump { X: -638, Y: -868, Triangle: 15, Duration: 17 } };
            reverseJump |= request is { Entity: 14, Priority: 6, ProgramCounter: 0xA0E, Command: FieldActorJump { Triangle: 56 } };
        };
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, hasReachedGoal: () => session.World is null && session.MapId == 709, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 15 && controller.Running && controller.Error is null && !forwardJump && session.Host.Process.GlobalCommand != Ff7GlobalCommand.Battle)
        {
            controller.Advance(1);
        }
        Console.WriteLine($"Native bridge continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, forwardJump={forwardJump}, reverseJump={reverseJump}, field={session.MapId}, status={policy.Status}, error={controller.Failure?.Message ?? controller.Error}");
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) || controller.Error is not null || !forwardJump || reverseJump || session.MapId != 703 || session.World is not null)
        {
            throw new InvalidOperationException("The native component route did not admit its shortest forward jump.", controller.Failure);
        }
    }
}
