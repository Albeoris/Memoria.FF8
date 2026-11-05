using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum GameplaySavePartyMovementKind
{
    Join,
    Split
}

public sealed record GameplaySavePartyMovementTarget([property: JsonRequired] int ObjectId, [property: JsonRequired] GameplaySaveActorPosition Destination, [property: JsonRequired] int Speed);

/// <summary>Consumed global party movement targets; each actor's pose remains the sole current-position owner.</summary>
public sealed record GameplaySavePartyMovement([property: JsonRequired] GameplaySavePartyMovementKind Kind, [property: JsonRequired] long FiberSequence, [property: JsonRequired] int CallerObjectId, [property: JsonRequired] GameplaySavePartyMovementTarget[] Targets)
{
    public void Validate(GameplaySaveFieldScripts scripts, GameplaySaveFieldScene scene, GameplaySaveFieldActors actors)
    {
        if (!Enum.IsDefined(Kind) || FiberSequence <= 0 || CallerObjectId < 0 || Targets is null || Targets.Length > (Kind == GameplaySavePartyMovementKind.Join ? 2 : 3) || Kind == GameplaySavePartyMovementKind.Split && Targets.Length == 0 || Targets.Any(target => target is null || target.ObjectId < 0 || target.Destination is null || target.Speed is <= 0 or > short.MaxValue || new[] { target.Destination.X, target.Destination.Y, target.Destination.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue)) || Targets.Select(target => target.ObjectId).Distinct().Count() != Targets.Length)
        {
            throw new InvalidDataException("Gameplay party movement contains invalid targets or ownership.");
        }
        GameplaySaveScriptWait wait = Kind == GameplaySavePartyMovementKind.Join ? GameplaySaveScriptWait.PartyJoin : GameplaySaveScriptWait.PartySplit;
        GameplaySaveScriptFiber? caller = scripts.Fibers.SingleOrDefault(fiber => fiber.Sequence == FiberSequence);
        int[] bindings = scene.SavedPartyBindings ?? scene.PartyBindings;
        if (bindings.Any(binding => binding != 255 && binding >= actors.RegisteredActors.Length))
        {
            throw new InvalidDataException("Gameplay saved party bindings refer to an absent registered actor.");
        }
        int[] bound = bindings.Where(binding => binding != 255).Select(binding => actors.RegisteredActors[binding]).ToArray();
        int[] expected = Kind == GameplaySavePartyMovementKind.Join ? bound.Skip(1).ToArray() : bound;
        if (caller is null || caller.ObjectId != CallerObjectId || caller.Complete || caller.Wait != wait || scripts.Fibers.Count(fiber => fiber.Wait is GameplaySaveScriptWait.PartyJoin or GameplaySaveScriptWait.PartySplit) != 1 || Kind == GameplaySavePartyMovementKind.Join && (bound.Length == 0 || bound[0] != CallerObjectId) || !expected.SequenceEqual(Targets.Select(target => target.ObjectId)))
        {
            throw new InvalidDataException("Gameplay party movement does not match its held caller and saved party bindings.");
        }
        foreach (GameplaySavePartyMovementTarget target in Targets)
        {
            GameplaySaveFieldActor actor = actors.Actors.Single(actor => actor.ObjectId == target.ObjectId);
            if (actor.Pose.FixedPosition is null || actor.Control.Completion is null or > 2 || actor.Movement is not null || Kind == GameplaySavePartyMovementKind.Split && (target.Speed is not (2696 or 7929) || new[] { target.Destination.X, target.Destination.Y, target.Destination.Z }.Any(value => (value & 4095) != 0)))
            {
                throw new InvalidDataException("Gameplay party movement lacks its canonical actor placement or a supported movement owner.");
            }
        }
    }
}
