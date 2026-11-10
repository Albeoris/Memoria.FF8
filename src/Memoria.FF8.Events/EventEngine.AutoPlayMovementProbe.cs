using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public bool AutoPlayFastMovement { get; set; }
    private float autoPlayMovementDistance;

    public bool HasAutoPlayHeldContact(int objectId)
    {
        return AutoPlayHeldContactScript(objectId) is not null && actors[objectId].LineContact;
    }

    public bool IsAutoPlayHeldContactReturn(ScriptCommand command)
    {
        return command.Name == "RET" && AutoPlayHeldContactScript(command.ObjectId)?.Id == command.ScriptId;
    }

    private EventScript? AutoPlayHeldContactScript(int objectId)
    {
        if (program is null || (uint)objectId >= actors.Count)
        {
            return null;
        }
        EventObject obj = program.Objects[objectId];
        return obj.IsDoor && obj.Scripts.Count > 5 && !EmptyNativeLineScript(actors[objectId], 5) ? obj.Scripts[5] : null;
    }

    /// <summary>Reports queued contact events and active contact scripts, independently of held line contact.</summary>
    public bool HasPendingAutoPlayInteraction(int objectId)
    {
        if (program is null || (uint)objectId >= actors.Count)
        {
            return false;
        }
        ActorState actor = actors[objectId];
        EventObject obj = program.Objects[objectId];
        bool HasBody(int local) => (uint)local < obj.Scripts.Count && !EmptyNativeLineScript(actor, local);
        if (obj.IsDoor)
        {
            byte events = Scene.NativeDoorPendingEvents?.GetValueOrDefault(objectId) ?? 0;
            // Holding contact can request local 5 on subsequent callbacks without
            // a queued event. Autoplay must be able to move after that request settles.
            if (events != 0 && actor.LineEnabled && IsUserControlEnabled && NativeDoorRequestedLocals(events, actor.LineContact).Any(HasBody))
            {
                return true;
            }
        }
        else if (actor.NativePushContactPending && HasBody(3) || actor.DoorLineEntryPending && HasBody(2))
        {
            return true;
        }
        foreach (Fiber fiber in fibers.Where(fiber => !fiber.Complete && !fiber.Initialization && fiber.ObjectId == objectId))
        {
            for (int local = 2; local < Math.Min(obj.IsDoor ? 8 : obj.IsArea ? 3 : 4, obj.Scripts.Count); local++)
            {
                if (fiber.Script.Id == obj.Scripts[local].Id && HasBody(local) && (obj.IsDoor ? fiber.NativeDoorSlot == 9 - local : fiber.NativeActorSlot == (obj.IsArea || local == 3 ? 6 : 7)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public float AutoPlayMovementStepDistance => AutoPlayMovementProbeSpeed / 256f;

    /// <summary>Finds a waiting native MOVE whose next step is obstructed by the current player.</summary>
    public NativeScriptMovementState? FindAutoPlayMoveBlockedByPlayer()
    {
        if (!CanAcceptMovementInput || PlayerActor is not { CollisionEnabled: true } player)
        {
            return null;
        }
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState actor = actors[objectId];
            if (actor == player || actor.NativeMovementCompletion542 != 1 || actor.NativeScriptMovement is not { Active: true, Suspended: false, Asynchronous: false, TargetNativeObjectId: null } move ||
                SelectScheduledFiber(objectId) is not { WaitReason: WaitReason.NativeScriptMovement } fiber || fiber.Sequence != move.FiberSequence)
            {
                continue;
            }
            NativeScriptMovementState source = move with { FacingDirection = (byte)actor.Direction };
            var (blocked, completion, _) = PrepareNativeScriptStep(actor, source);
            if (completion != 1 || blocked.FixedX != source.FixedX || blocked.FixedY != source.FixedY)
            {
                continue;
            }
            var (clear, _, _) = PrepareNativeScriptStep(actor, source, ignoredContact: player.ObjectId);
            if (clear.FixedX != source.FixedX || clear.FixedY != source.FixedY)
            {
                return move;
            }
        }
        return null;
    }

    private int AutoPlayFastStepSpeed(ActorState player, FieldFixedPosition position, int speed, float targetDistance)
    {
        if (!AutoPlayFastMovement || !CanAcceptMovementInput || services.IsMoviePlaying || targetDistance * 256 <= speed)
        {
            return 0;
        }
        int fast = (int)Math.Min(7929 * PlayerMovementSpeed.AutoPlayTranslationMultiplier, Math.Min(speed * PlayerMovementSpeed.AutoPlayTranslationMultiplier, targetDistance * 256));
        Vector2 current = new(position.X / 4096f, position.Y / 4096f);
        float translation = fast / 256f;
        float contactReach = translation + player.PushRadius + 2;
        bool NearLine(Vector3 start, Vector3 end)
        {
            return current.X >= Math.Min(start.X, end.X) - contactReach && current.X <= Math.Max(start.X, end.X) + contactReach
                && current.Y >= Math.Min(start.Y, end.Y) - contactReach && current.Y <= Math.Max(start.Y, end.Y) + contactReach;
        }
        bool NearArea(InfAreaTrigger area)
        {
            // INF area modes also evaluate the side of the extended line.
            Vector2 edge = new(area.End.X - area.Start.X, area.End.Y - area.Start.Y);
            return NearLine(area.Start, area.End) || Math.Abs(edge.X * (current.Y - area.Start.Y) - edge.Y * (current.X - area.Start.X)) <= contactReach * edge.Length();
        }
        if (InfGateways.Any(gate => gate.Destination.FieldId != 0x7FFF && NearLine(gate.Start, gate.End)) || InfAreaTriggers.Any(area => area.AreaIndex != 0xFF && NearArea(area)))
        {
            return 0;
        }
        foreach (ActorState actor in actors)
        {
            if (actor.LineEnabled && (actor.LineContact || actor.LineStart is not Vector3 start || actor.LineEnd is not Vector3 end || NearLine(start, end)))
            {
                return 0;
            }
        }
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState peer = actors[objectId];
            if (peer.ObjectId == player.ObjectId || peer.ModelId is -1 or 65535 || !peer.CollisionEnabled && !peer.PushEnabled)
            {
                continue;
            }
            float reach = contactReach + (player.PushRadius + peer.PushRadius) / 2f;
            if (!(Vector2.DistanceSquared(current, new(peer.X, peer.Y)) > reach * reach))
            {
                return 0;
            }
        }
        return fast;
    }

    private int AutoPlayMovementProbeSpeed
    {
        get
        {
            // Predict the upcoming sampler, including physical holds and pending
            // pulses, rather than retaining a preceding Cancel's sampled walk bit.
            var input = ButtonInput ?? throw new NotSupportedException("Autoplay walking requires the native input sampler.");
            ushort held = services.InputConfiguration?.Map((ushort)(input.PhysicalHeld | input.PendingPulse)) ?? throw new NotSupportedException("Autoplay walking requires the native input configuration.");
            int speed = GetNativePlayerMovementSpeed(!Scene.RunEnabled || (held & 0x10) != 0);
            return services.IsMoviePlaying ? speed : Memoria.FF8.Runtime.PlayerMovementSpeed.Scale(speed, PlayerMovementSpeedPercent);
        }
    }

    /// <summary>Inverts the field's input rotation without changing its native direction state.</summary>
    public Vector2 AutoPlayMovementAxes(Vector2 worldDelta)
    {
        if (worldDelta == Vector2.Zero)
        {
            return Vector2.Zero;
        }
        float angle = MathF.Atan2(worldDelta.X, -worldDelta.Y) - (InputAxisDirection + PlayerInputDirectionOffset) * MathF.PI / 128;
        return new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
    }

    /// <summary>Predicts one ordinary step against current geometry and peers, without publishing movement or preparing followers.</summary>
    public NativePlayerMovementResult ProbeAutoPlayMovement(FieldFixedPosition position, int triangle, Vector2 worldDelta)
    {
        ActorState player = PlayerActor ?? throw new InvalidOperationException("Walking requires a current player actor.");
        if (ResolveNativePlayerMovement is null || CanUseNativePlayerMovement?.Invoke(player) != true)
        {
            throw new NotSupportedException("Autoplay walking requires the certified native player movement adapter.");
        }
        byte retry = Scene.NativePlayerEarlyRetryState is byte known && known <= 1 ? known : throw new NotSupportedException("Autoplay walking requires preserved native movement retry state.");
        Vector2 axes = AutoPlayMovementAxes(worldDelta);
        if (axes.LengthSquared() > 1)
        {
            axes = Vector2.Normalize(axes);
        }
        int direction = axes == Vector2.Zero ? (byte)player.Direction : (byte)(NativePartyDirection(0, 0, (int)(axes.X * 4096), (int)(axes.Y * 4096)) + InputAxisDirection + PlayerInputDirectionOffset);
        int speed = AutoPlayMovementProbeSpeed;
        int fastSpeed = AutoPlayFastStepSpeed(player, position, speed, worldDelta.Length());
        NativePlayerMovementQuery query = new(player.ObjectId, triangle, position.X, position.Y, position.Z, direction, (short)speed, player.PushRadius, retry, probe => EvaluateNativePlayerGroundContacts(player, probe).Collision, PlayerMovementSpeedPercent, fastSpeed);
        return ResolveNativePlayerMovement(query) ?? throw new NotSupportedException("Autoplay walking encountered an unsupported native movement probe.");
    }

    public bool HasAutoPlayDoorCrossingTransition(int objectId)
    {
        return IsNativeDoorRequest(objectId) && program!.Objects[objectId].Scripts.Count > 4 &&
            program.Objects[objectId].Scripts[4].ReachableOperations.Any(index => program.Operations[index].Opcode is Jsm.Opcode.MAPJUMP or Jsm.Opcode.MAPJUMP3);
    }

    /// <summary>Checks the native crossing query for a line the planner intends to walk around.</summary>
    public bool CrossesAutoPlayDoor(FieldFixedPosition from, FieldFixedPosition to, int objectId)
    {
        ActorState line = actors[objectId];
        if (!line.LineEnabled || line.LineStart is not Vector3 start || line.LineEnd is not Vector3 end || PlayerActor is not { } player)
        {
            return false;
        }
        Vector3 current = new(from.X >> 12, from.Y >> 12, from.Z >> 12);
        Vector3 proposed = new(to.X >> 12, to.Y >> 12, from.Z >> 12);
        int distance = NativeInfDistance(start, end, proposed, out _);
        if (distance < -1)
        {
            throw new NotSupportedException("Native autoplay door distance overflow is unsupported.");
        }
        int radius = (ushort)player.PushRadius;
        return distance >= 0 && distance < (long)radius * radius && NativeInfCrosses(start, end, current, proposed);
    }

    /// <summary>Observes a real callback while preserving the host's existing movement and gateway observers.</summary>
    public (bool? MovementAccepted, int? GatewayIndex) ObserveAutoPlayMovement(Action callback, float targetDistance = 0)
    {
        Action<bool>? previousMovement = NativePlayerMovementProcessed;
        Action<int>? previousGateway = observeInfGatewayAdmission;
        bool? accepted = null;
        int? gateway = null;
        float previousDistance = autoPlayMovementDistance;
        autoPlayMovementDistance = targetDistance;
        NativePlayerMovementProcessed = value =>
        {
            accepted = value;
            previousMovement?.Invoke(value);
        };
        observeInfGatewayAdmission = value =>
        {
            gateway = value;
            previousGateway?.Invoke(value);
        };
        try
        {
            callback();
            return (accepted, gateway);
        }
        finally
        {
            NativePlayerMovementProcessed = previousMovement;
            observeInfGatewayAdmission = previousGateway;
            autoPlayMovementDistance = previousDistance;
        }
    }
}
