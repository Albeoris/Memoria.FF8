using System.Numerics;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class BalambTestJourney
{
    internal static void OpeningToGate(GameSession session, GameWorld world, bool encounters = false, bool fullCombat = true)
    {
        world.EnsureField();
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: fullCombat));
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigureEncounters, RandomEncounters: encounters));
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Field?.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        Apply(world, new(PlayerActionKind.Move, Horizontal: -1));
        Apply(world, new(PlayerActionKind.Move, Horizontal: -1));
        for (int callback = 0; callback < 3000 && !session.IsMoviePlaying; callback++)
        {
            Apply(world, world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable ? new(PlayerActionKind.Move, Vertical: 1) : Automatic(session, world));
        }
        Assert.True(session.IsMoviePlaying);
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Field?.Id == 232 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        Apply(world, new(PlayerActionKind.Move, Vertical: -1, Frames: 90));
        Apply(world, new(PlayerActionKind.Move, Horizontal: -1, Frames: 30));
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Events.Actors.Where(actor => actor.ObjectId is 14 or 15 or 16).All(actor => actor.NativeScriptMovement is not { Active: true } && actor.MovementTarget is null));
        IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, new Vector2(500, world.Events.PlayerActor!.Y));
        IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, new Vector2(650, -2850));
        IfritCausalRecreationTests.WalkDirectToInteraction(world, session, 11);
        Apply(world, new(PlayerActionKind.Interact, 11));
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        foreach (int destination in new[] { 139, 137, 172, 165, 163, 162, 161, 160, 159 })
        {
            IfritCausalRecreationTests.WalkGateway(world, session, destination);
        }
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Events.FieldId == 159 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
        IfritCausalRecreationTests.WalkDirectToInteraction(world, session, 6, () => world.Events.AcquiredGuardianForces!.Contains(0) && world.Events.AcquiredGuardianForces!.Contains(1));
        IfritCausalRecreationTests.DriveUntil(world, session, () => world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable);
    }

    internal static void Apply(GameWorld world, PlayerAction action)
    {
        PlayerActionResult result = world.Apply(action);
        Assert.True(result.Accepted, $"Actual Balamb input refused: {action}; {result.Detail}");
    }

    internal static void WalkRoutedPoint(GameSession session, GameWorld world, Vector2 target)
    {
        var mesh = world.Field!.Walkmesh;
        Assert.True(mesh.TryPosition(target.X, target.Y, out _, out int destination));
        int source = world.Events.PlayerActor!.WalkmeshTriangle;
        Dictionary<int, int> parents = [];
        HashSet<int> visited = [source];
        Queue<int> pending = new([source]);
        while (pending.TryDequeue(out int triangle) && !visited.Contains(destination))
        {
            var current = mesh.Triangles[triangle];
            foreach (int neighbor in new[] { current.NeighborA, current.NeighborB, current.NeighborC })
            {
                if (neighbor >= 0 && !world.Events.Scene.LockedTriangles.Contains(neighbor) && visited.Add(neighbor))
                {
                    parents.Add(neighbor, triangle);
                    pending.Enqueue(neighbor);
                }
            }
        }
        Assert.Contains(destination, visited);
        List<int> route = [destination];
        while (route[^1] != source)
        {
            route.Add(parents[route[^1]]);
        }
        route.Reverse();
        foreach (int triangle in route.Skip(1))
        {
            var current = mesh.Triangles[triangle];
            IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, new((current.A.X + current.B.X + current.C.X) / 3, (current.A.Y + current.B.Y + current.C.Y) / 3));
        }
        IfritCausalRecreationTests.WalkAdvisoryPoint(world, session, target);
    }

    private static PlayerAction Automatic(GameSession session, GameWorld world)
    {
        DialogueState? dialogue = world.Events.CurrentDialogue;
        return session.IsMoviePlaying ? new(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int first ? new(PlayerActionKind.Choose, first) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
    }
}
