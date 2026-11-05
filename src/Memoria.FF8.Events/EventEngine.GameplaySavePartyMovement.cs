using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private GameplaySavePartyMovement? CaptureGameplayPartyMovement()
    {
        ValidateNativePartyMovement(actors, Scene, fibers, partyJoin, partySplit);
        if (partyJoin is null && partySplit is null)
        {
            return null;
        }
        PartyJoinTarget[] targets = partyJoin?.Targets ?? partySplit!.Targets;
        if (targets.Any(target => actors[target.ObjectId].NativeScriptMovement is not null || actors[target.ObjectId].NativeMovementRequest is not null))
        {
            throw new NotSupportedException("Gameplay party movement requires separate ownership for a displaced or backed-up MOVE.");
        }
        return new(partyJoin is null ? GameplaySavePartyMovementKind.Split : GameplaySavePartyMovementKind.Join, partyJoin?.FiberSequence ?? partySplit!.FiberSequence, partyJoin?.CallerObjectId ?? partySplit!.CallerObjectId, targets.Select(target => new GameplaySavePartyMovementTarget(target.ObjectId, new(target.TargetX, target.TargetY, target.TargetZ), target.Speed)).ToArray());
    }

    private void RestoreGameplayPartyMovement(GameplaySavePartyMovement? saved)
    {
        if (saved is null)
        {
            return;
        }
        if (ResolvePartyTraceTriangle is null || ResolveNativeJoinMovement is null || ResolveNativeScriptDirection is null)
        {
            throw new NotSupportedException("Gameplay party movement requires the current native geometry services.");
        }
        PartyJoinTarget[] targets = saved.Targets.Select(target =>
        {
            ActorState actor = actors[target.ObjectId];
            FieldFixedPosition raw = ReadNativeScriptPosition(actor) ?? throw new InvalidDataException("Gameplay party movement lost its canonical actor placement.");
            if (CanUseNativeJoinMovement?.Invoke(actor) != true)
            {
                throw new NotSupportedException("Gameplay party movement requires a certified current movement adapter.");
            }
            actor.MovementStopDistance = saved.Kind == GameplaySavePartyMovementKind.Join ? 96 : 8;
            return new PartyJoinTarget(target.ObjectId, raw.X, raw.Y, raw.Z, target.Destination.X, target.Destination.Y, target.Destination.Z, target.Speed);
        }).ToArray();
        partyJoin = saved.Kind == GameplaySavePartyMovementKind.Join ? new(saved.FiberSequence, saved.CallerObjectId, targets) : null;
        partySplit = saved.Kind == GameplaySavePartyMovementKind.Split ? new(saved.FiberSequence, saved.CallerObjectId, targets) : null;
    }

}
