using System.Numerics;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Empty native contact slots still dispatch normally, but do not interrupt an autoplay walk.</summary>
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
            if (NativeDoorRequestedLocals(events, actor.LineContact).Any(HasBody))
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
        NativePlayerMovementQuery query = new(player.ObjectId, triangle, position.X, position.Y, position.Z, direction, (short)AutoPlayMovementProbeSpeed, player.PushRadius, retry, probe => EvaluateNativePlayerGroundContacts(player, probe).Collision, PlayerMovementSpeedPercent);
        return ResolveNativePlayerMovement(query) ?? throw new NotSupportedException("Autoplay walking encountered an unsupported native movement probe.");
    }

    /// <summary>Observes a real callback while preserving the host's existing movement and gateway observers.</summary>
    public (bool? MovementAccepted, int? GatewayIndex) ObserveAutoPlayMovement(Action callback)
    {
        Action<bool>? previousMovement = NativePlayerMovementProcessed;
        Action<int>? previousGateway = observeInfGatewayAdmission;
        bool? accepted = null;
        int? gateway = null;
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
        }
    }
}
