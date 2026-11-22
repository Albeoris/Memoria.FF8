using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMineGatewayProgressTests
{
    internal static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_MINE_ROUTE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original mine gateway progression: configure resources and an external standalone continuation.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7 ?? throw new InvalidDataException("The mine route fixture requires a native field continuation.");
        string[] existingRoutes = saved.AutoPlay?.Coverage.NavigationRoutes.Select(route => JsonSerializer.Serialize(route)).ToArray() ?? [];
        Check(saved.World is null && saved.AutoPlay is { DestinationField: 384, Active: { Entity: 1, Script: 2, Accepted: true, Returned: false } } && saved.Process.Actors.Single(actor => actor.PlayerControlled).Ladder is { Completed: false, TargetTriangle: 27 }, "The fixture must retain its actual accepted downward ladder and airport destination.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The bounded mine route requested an unrelated choice checkpoint."));
        Dictionary<int, int> admissions = new() { [1] = 0, [2] = 0 };
        HashSet<int> completedLadders = [];
        Ff7NativeGatewayDeparture? departure = null;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission is { Accepted: true, Started: true, Priority: 1, Script: 2 } && admissions.ContainsKey(admission.Entity))
            {
                admissions[admission.Entity]++;
            }
        };
        session.NativeFieldDeparting += actual => departure = actual;
        Ff7AutoplayDriver? liveDriver = null;
        Ff7AutoPlayController controller = new(session, driver =>
        {
            liveDriver = driver;
            return policy.ChooseInput(driver);
        }, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        int polls = 0;
        List<string> poses = [];
        while (controller.Running && session.World is null && session.Battle is null && polls < 1200 && timer.Elapsed.TotalSeconds < 30)
        {
            controller.Advance(1);
            polls++;
            if (polls % 100 == 0 && session.World is null && session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is { } sampled)
            {
                poses.Add($"{polls}:{sampled.X4096},{sampled.Y4096},{sampled.Z4096}/t{sampled.Triangle}/d{sampled.Direction}");
            }
            if (session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled)?.Ladder is { Completed: true } ladder)
            {
                completedLadders.Add(ladder.TargetTriangle);
            }
        }
        Ff7Actor? terminalPlayer = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        var gateway = session.Field.Triggers.Gateways[2];
        int distance = terminalPlayer is null ? -1 : Ff7Session.DistanceToSegment(gateway.A, gateway.B, new Vector3(terminalPlayer.X4096 >> 12, terminalPlayer.Y4096 >> 12, terminalPlayer.Z4096 >> 12), out _);
        string terminal = $"battle={session.Battle?.Formation.FormationId}/{session.Battle?.Outcome}/ready{session.Battle?.ReadyPlayer}; global={session.Host.Process.GlobalCommand}; tick={session.Machine.Tick}; fieldTick={session.Host.Process.Tick}; pendingTick={session.HasPendingTick}; pendingCycle={session.Machine.CycleCursor is not null}; player={terminalPlayer?.X4096},{terminalPlayer?.Y4096},{terminalPlayer?.Z4096}/t{terminalPlayer?.Triangle}/d{terminalPlayer?.Direction}; gateDistanceSquared={distance}; pending={liveDriver?.PlanningPending}; route={policy.State.NativeRoute?.Cursor}/{policy.State.NativeRoute?.Steps.Length}; work={policy.WorkProgress}; poses={string.Join(';', poses)}";
        bool departed = session.World is { } world && world.Process.EntrySelector == 5 && departure is { GatewayIndex: 2, Transition.Map: 5 };
        bool encountered = session.Battle is { Outcome: Ff7BattleOutcome.Fighting } && session.Host.Process.GlobalCommand == Ff7GlobalCommand.Battle && terminalPlayer is { Triangle: not (27 or 59) } && policy.State.NavigationGoal?.EventId == "gateway:2";
        Check(controller.Error is null && admissions[1] == 0 && (departed || encountered), $"Normal mine routing did not progress beyond its completed ladder pair to an authenticated world departure or actual encounter: error={controller.Error}; polls={polls}; native={controller.Steps}; status={policy.State.Status}; Push1={admissions[1]}; Push2={admissions[2]}; ladders={string.Join(',', completedLadders)}; {terminal}.");
        Check(completedLadders.Contains(27) && completedLadders.Contains(59) && admissions[2] > 0, "The route bypassed its actual pending downward ladder or subsequent admitted upward prerequisite.");
        HashSet<string> retainedRoutes = policy.State.Coverage.NavigationRoutes.Select(route => JsonSerializer.Serialize(route)).ToHashSet(StringComparer.Ordinal);
        Check(policy.State.DestinationField == 384 && session.FieldGeneration >= saved.FieldGeneration && existingRoutes.All(retainedRoutes.Contains), "Native mine departure discarded explicit intent or existing route history.");
        Check(SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "The bounded mine continuation changed its authoritative standalone save.");
        Console.WriteLine($"PASS original mine route {(departed ? "native World5 departure" : "genuine battle boundary; full exit requires the application combat owner")}: polls={polls}, callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, selector={session.World?.Process.EntrySelector}, battle={session.Battle?.Formation.FormationId}, Push1={admissions[1]}, Push2={admissions[2]}, completedLadders={string.Join(',', completedLadders.Order())}, source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
