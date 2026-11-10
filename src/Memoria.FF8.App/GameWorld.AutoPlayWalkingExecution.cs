using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed record AutoPlayWalkResult(bool Arrived, bool Interrupted, int? GatewayIndex = null, bool ContactTriggered = false, int? InterruptingObjectId = null);

internal sealed partial class GameWorld
{
    internal bool StepAsideForAutoPlay(NativeScriptMovementState move)
    {
        if (Events.PlayerActor is not ActorState player || Events.Scene.NativePlayerPosition is not FieldFixedPosition position)
        {
            return false;
        }
        Vector2 start = new(move.FixedX / 4096f, move.FixedY / 4096f);
        Vector2 segment = new Vector2(move.TargetX / 4096f, move.TargetY / 4096f) - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared == 0)
        {
            return false;
        }
        float Clearance(Vector2 point)
        {
            float along = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0, 1);
            return Vector2.DistanceSquared(point, start + along * segment);
        }
        float best = Clearance(new(position.X / 4096f, position.Y / 4096f));
        Vector2? selected = null;
        for (int heading = 0; heading < 16; heading++)
        {
            float angle = heading * MathF.PI / 8;
            Vector2 delta = new(MathF.Sin(angle), -MathF.Cos(angle));
            NativePlayerMovementResult probe = Events.ProbeAutoPlayMovement(position, player.WalkmeshTriangle, delta);
            if (!probe.Accepted)
            {
                continue;
            }
            float clearance = Clearance(new(probe.FixedX / 4096f, probe.FixedY / 4096f));
            if (clearance > best)
            {
                best = clearance;
                selected = delta;
            }
        }
        if (selected is not Vector2 direction)
        {
            return false;
        }
        // Publish an ordinary input callback and leave all collision and script state native.
        StepAutoPlayWalking(direction, Events.FieldId, [], -1, null, out bool moved, allowFast: false);
        return moved;
    }

    public AutoPlayWalkResult WalkAutoPlayApproach(AutoPlayTarget target, Action onProgress, AutoPlayIdleDeadline? idleDeadline = null)
    {
        int field = Events.FieldId;
        HashSet<int> pending = Events.Actors.Where(actor => Events.HasPendingAutoPlayInteraction(actor.ObjectId)).Select(actor => actor.ObjectId).ToHashSet();
        Vector2 destination = new(target.ApproachX ?? target.X, target.ApproachY ?? target.Y);
        int approachObjectId = target.ObjectId;
        AutoPlayTarget? contactTarget = target.Kind == AutoPlayTargetKind.Talk ? null : target;
        for (int frame = 0; frame < 20_000; frame++)
        {
            if (Events.FieldId != field || NextTrigger.Kind != PlayerTriggerKind.Playable || !Events.CanAcceptMovementInput)
            {
                return new(false, true);
            }
            if (TryFindAutoPlayWalkingRoute(target, out AutoPlayWalkingRoute route, idleDeadline))
            {
                return WalkAutoPlayRoute(route, onProgress, approachObjectId, approachTarget: contactTarget);
            }
            // A frozen peer layout cannot prove that future party-following steps
            // are unavailable. Publish only a certified local advance, then plan
            // again against the peers actually moved by the native callback.
            FieldFixedPosition position = Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("Autoplay approach requires a retained native player position.");
            Vector2 location = new(position.X / 4096f, position.Y / 4096f);
            float distance = Vector2.DistanceSquared(location, destination);
            Vector2? advance;
            using (idleDeadline?.Suspend())
            {
                advance = FindAutoPlayWalkingAdvance(position, Events.PlayerActor!.WalkmeshTriangle, destination, Events.ProbeAutoPlayMovement);
            }
            if (advance is null)
            {
                return new(false, false);
            }
            AutoPlayWalkResult step = StepAutoPlayWalking(advance.Value, field, pending, approachObjectId, contactTarget, out bool moved);
            if (step.Interrupted)
            {
                return step;
            }
            FieldFixedPosition actual = Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("Autoplay approach requires the executed native player position.");
            if (!moved || Vector2.DistanceSquared(new(actual.X / 4096f, actual.Y / 4096f), destination) >= distance)
            {
                return new(false, false);
            }
            onProgress();
        }
        throw new NotSupportedException("Autoplay approach exceeded its bounded movement callback budget.");
    }

    internal static Vector2? FindAutoPlayWalkingAdvance(FieldFixedPosition position, int triangle, Vector2 destination, Func<FieldFixedPosition, int, Vector2, NativePlayerMovementResult> probe)
    {
        Vector2 location = new(position.X / 4096f, position.Y / 4096f);
        Vector2 direct = destination - location;
        float originalDistance = Vector2.DistanceSquared(location, destination);
        float Distance(NativePlayerMovementResult result) => result.Accepted ? Vector2.DistanceSquared(new(result.FixedX / 4096f, result.FixedY / 4096f), destination) : float.PositiveInfinity;
        if (Distance(probe(position, triangle, direct)) < originalDistance)
        {
            return direct;
        }
        Vector2? selected = null;
        float bestDistance = originalDistance;
        for (int direction = 0; direction <= byte.MaxValue; direction++)
        {
            float angle = direction * MathF.PI / 128;
            Vector2 delta = new(MathF.Sin(angle), -MathF.Cos(angle));
            float distance = Distance(probe(position, triangle, delta));
            if (distance < bestDistance)
            {
                selected = delta;
                bestDistance = distance;
            }
        }
        return selected;
    }

    public AutoPlayWalkResult WalkAutoPlayRoute(AutoPlayWalkingRoute route, Action onProgress, int approachObjectId = -1, AutoPlayTarget? contact = null, AutoPlayTarget? approachTarget = null)
    {
        int field = Events.FieldId;
        HashSet<int> pending = Events.Actors.Where(actor => Events.HasPendingAutoPlayInteraction(actor.ObjectId)).Select(actor => actor.ObjectId).ToHashSet();
        int frames = 0;
        foreach (WalkmeshPosition waypoint in route.Waypoints)
        {
            HashSet<(int X, int Y, int Triangle)> visited = [];
            float bestDistance = float.PositiveInfinity;
            while (true)
            {
                if (Events.FieldId != field || NextTrigger.Kind != PlayerTriggerKind.Playable || !Events.CanAcceptMovementInput)
                {
                    return new(false, true);
                }
                FieldFixedPosition position = Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("Autoplay walking requires a retained native player position.");
                ActorState player = Events.PlayerActor!;
                Vector2 delta = new(waypoint.Position.X - position.X / 4096f, waypoint.Position.Y - position.Y / 4096f);
                float distance = delta.Length();
                if (contact is null && AutoPlayWalkingWaypointReached(Field!.Walkmesh, position, player.WalkmeshTriangle, waypoint, Math.Max(0.125f, Events.AutoPlayMovementStepDistance * 0.75f)))
                {
                    break;
                }
                if (!visited.Add((position.X, position.Y, player.WalkmeshTriangle)))
                {
                    return new(false, false);
                }
                if (++frames > 20_000)
                {
                    throw new NotSupportedException("Autoplay walking exceeded its bounded movement callback budget.");
                }
                if (delta == Vector2.Zero && contact is not null)
                {
                    float angle = contact.Direction * MathF.PI / 128;
                    delta = new(MathF.Sin(angle), -MathF.Cos(angle));
                }
                AutoPlayWalkResult step = StepAutoPlayWalking(delta, field, pending, approachObjectId, contact ?? approachTarget, out bool moved);
                if (step.Interrupted)
                {
                    return step;
                }
                if (!moved)
                {
                    return new(false, false);
                }
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    onProgress();
                }
            }
        }
        return new(true, false);
    }

    private AutoPlayWalkResult StepAutoPlayWalking(Vector2 delta, int field, HashSet<int> pending, int approachObjectId, AutoPlayTarget? contact, out bool moved, bool allowFast = true)
    {
        Vector2 axes = Events.AutoPlayMovementAxes(delta);
        (bool? accepted, int? gateway) = Events.ObserveAutoPlayMovement(() => Apply(new(PlayerActionKind.Move, Horizontal: axes.X, Vertical: axes.Y)), allowFast ? delta.Length() : 0);
        moved = accepted == true;
        bool contacted = contact is { ObjectId: >= 0 } && Events.HasPendingAutoPlayInteraction(contact.ObjectId);
        int[] owners = Events.Actors.Where(actor => actor.ObjectId != approachObjectId && !pending.Contains(actor.ObjectId) && Events.HasPendingAutoPlayInteraction(actor.ObjectId)).Select(actor => actor.ObjectId).ToArray();
        bool interrupted = contacted || gateway is not null || Events.FieldId != field || NextTrigger.Kind != PlayerTriggerKind.Playable || !Events.CanAcceptMovementInput || Events.FieldFade?.Physical.Mode == 4 || owners.Length != 0;
        return new(false, interrupted, gateway, contacted, owners.Length == 1 ? owners[0] : null);
    }

    public bool IsAutoPlayTalkInReach(int objectId)
    {
        ActorState player = Events.PlayerActor!;
        ActorState actor = Events.Actors[objectId];
        long dx = (long)MathF.Floor(actor.X) - (long)MathF.Floor(player.X);
        long dy = (long)MathF.Floor(actor.Y) - (long)MathF.Floor(player.Y);
        int dz = (int)MathF.Floor(player.Z) - (int)MathF.Floor(actor.Z);
        long squared = dx * dx + dy * dy;
        return squared is > 0 and <= int.MaxValue && dz is > -256 and < 256 && (int)Math.Sqrt(squared) < (ushort)player.PushRadius + (ushort)actor.TalkRadius;
    }

    public AutoPlayWalkResult FaceAutoPlayActor(int objectId)
    {
        ActorState player = Events.PlayerActor!;
        ActorState actor = Events.Actors[objectId];
        return FaceAutoPlayDirection(new(actor.X - player.X, actor.Y - player.Y), objectId);
    }

    public AutoPlayWalkResult FaceAutoPlayTarget(AutoPlayTarget target)
    {
        float angle = target.Direction * MathF.PI / 128;
        return FaceAutoPlayDirection(new(MathF.Sin(angle), -MathF.Cos(angle)), target.ObjectId);
    }

    private AutoPlayWalkResult FaceAutoPlayDirection(Vector2 delta, int objectId)
    {
        HashSet<int> pending = Events.Actors.Where(actor => Events.HasPendingAutoPlayInteraction(actor.ObjectId)).Select(actor => actor.ObjectId).ToHashSet();
        // The ordinary talk query arbitrates this actor's slot7 against its own
        // push slot6; unrelated contacts still interrupt the facing step.
        return StepAutoPlayWalking(delta, Events.FieldId, pending, objectId, null, out _, allowFast: false);
    }

    public AutoPlayWalkResult WalkAutoPlayContact(AutoPlayTarget target, Action onProgress)
    {
        // The final segment must publish the ordinary collision, line and gateway callbacks.
        // Push targets can stop against their peer envelope before reaching the sample itself.
        AutoPlayWalkingRoute crossing = new([new WalkmeshPosition(new Vector3(target.X, target.Y, target.Z), target.Triangle)]);
        return WalkAutoPlayRoute(crossing, onProgress, target.ObjectId, target);
    }
}
