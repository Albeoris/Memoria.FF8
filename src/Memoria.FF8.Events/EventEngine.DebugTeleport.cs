using System.Numerics;

namespace Memoria.FF8.Events;

public readonly record struct FieldTeleportPosition(float X, float Y, float Z, int Triangle, int Direction = 0);
public readonly record struct FieldTeleportContactResult(bool PlacementApplied, bool ContactTriggered, bool NativeInterrupted);

public sealed partial class EventEngine
{
    /// <summary>Explicit diagnostic placement. Contact dispatch uses the current field's normal geometric queries; it never requests a script directly.</summary>
    public bool TeleportPlayer(float x, float y, float z, int triangle, int direction = 0, bool dispatchContacts = false)
    {
        if (dispatchContacts)
        {
            return TeleportPlayerAndDispatchContacts(new(x, y, z, triangle, direction)).ContactTriggered;
        }
        return PlaceDiagnosticPlayer(x, y, z, triangle, direction, false, out _);
    }

    /// <summary>Runs the current native callback before applying diagnostic placement at the ordinary player-input boundary.</summary>
    public FieldTeleportContactResult TeleportPlayerAndDispatchContacts(FieldTeleportPosition target, FieldTeleportPosition? approach = null)
    {
        ValidateDiagnosticPosition(target);
        if (approach is FieldTeleportPosition initialApproach)
        {
            ValidateDiagnosticPosition(initialApproach);
        }
        if (!HasDiagnosticPlayerOwner())
        {
            throw new InvalidOperationException("Diagnostic placement requires a free controlled field leader.");
        }
        if (pendingNativePlayerInput is not null)
        {
            throw new InvalidOperationException("Diagnostic placement cannot replace an already staged player input.");
        }
        ValidateDiagnosticPartyOwners();
        bool applied = false;
        bool triggered = false;
        Action? commitContacts = null;
        Action placement = () =>
        {
            if (!HasDiagnosticPlayerOwner() || Scene.LockedTriangles.Contains(target.Triangle) || approach is FieldTeleportPosition prior && Scene.LockedTriangles.Contains(prior.Triangle))
            {
                return;
            }
            if (approach is FieldTeleportPosition previous)
            {
                PlaceDiagnosticPlayer(previous.X, previous.Y, previous.Z, previous.Triangle, previous.Direction, false, out _);
            }
            triggered = PlaceDiagnosticPlayer(target.X, target.Y, target.Z, target.Triangle, target.Direction, true, out commitContacts);
            applied = true;
        };
        pendingNativePlayerInput = placement;
        try
        {
            Step();
        }
        finally
        {
            if (ReferenceEquals(pendingNativePlayerInput, placement))
            {
                pendingNativePlayerInput = null;
            }
        }
        commitContacts?.Invoke();
        return new FieldTeleportContactResult(applied, triggered, !applied);
    }

    private static void ValidateDiagnosticPosition(FieldTeleportPosition position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z) || position.X is < short.MinValue or > short.MaxValue || position.Y is < short.MinValue or > short.MaxValue || position.Z is < short.MinValue or > short.MaxValue || position.Triangle is < 0 or > short.MaxValue || position.Direction is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Diagnostic placement requires bounded field coordinates, triangle and direction.");
        }
    }

    private bool HasDiagnosticPlayerOwner()
    {
        return program is not null && !transitionRequested && CanAcceptFieldMovement && !HasPendingBattle && PendingTripleTriad is null && Scene.NativeMenuTutorial is null && PlayerActor is ActorState player && !NativePlayerHasScriptedMode(player);
    }

    private bool PlaceDiagnosticPlayer(float x, float y, float z, int triangle, int direction, bool dispatchContacts, out Action? commitContacts)
    {
        commitContacts = null;
        ValidateDiagnosticPosition(new(x, y, z, triangle, direction));
        if (!HasDiagnosticPlayerOwner())
        {
            throw new InvalidOperationException("Diagnostic placement requires a free controlled field leader.");
        }
        ActorState player = PlayerActor!;
        int fixedX = checked((int)MathF.Round(x * 4096));
        int fixedY = checked((int)MathF.Round(y * 4096));
        int fixedZ = checked((int)MathF.Round(z * 4096));
        if (Scene.NativePartyTraceVersion == 1)
        {
            int height = ResolveNativeTriangleHeight?.Invoke(triangle, fixedX / 4096, fixedY / 4096) ?? throw new NotSupportedException("Native diagnostic placement requires the selected original triangle plane.");
            if (height is < short.MinValue or > short.MaxValue)
            {
                throw new NotSupportedException("Native diagnostic placement requires a signed-WORD plane height.");
            }
            // Native player ground placement retains fixed XY and publishes the integer47A680 plane height.
            fixedZ = height << 12;
        }
        FieldFixedPosition raw = new(fixedX, fixedY, fixedZ);
        x = raw.X / 4096f;
        y = raw.Y / 4096f;
        z = raw.Z / 4096f;
        WalkmeshPosition position = new(new Vector3(x, y, z), triangle);
        PartyTraceState? diagnosticTrace = null;
        if (Scene.NativePartyTraceVersion == 1)
        {
            ReadNativePartyPlayerPosition(player);
            ValidateDiagnosticPartyOwners();
            diagnosticTrace = PrepareDiagnosticPartyTrace(raw, triangle, direction);
            PreparePartyTraceFollowers(position, direction, true, diagnosticTrace);
        }
        int[] contacts = [];
        bool triggered = false;
        if (dispatchContacts)
        {
            if (Scene.NativePartyTraceVersion != 1 || Scene.NativeActorLayoutVersion != 1)
            {
                throw new NotSupportedException("Diagnostic contact dispatch requires the initialized native field and party trace.");
            }
            contacts = EvaluateNativePlayerGroundContacts(player, new NativePlayerGroundProbe(raw.X, raw.Y, raw.Z >> 12)).PushContacts;
            foreach (int objectId in contacts)
            {
                ValidateNativePushDispatch(actors[objectId]);
            }
            IReadOnlyList<NativeLineContactChange> lines = MovePlayerCore(x, y, z, triangle, raw, direction, true, raw, diagnosticTrace);
            triggered = transitionRequested || contacts.Length > 0 || actors.Any(actor => actor.DoorLineEntryPending) || lines.Any(line => NativeDoorRequestedLocals(line.PendingEvents, line.Contact).Any(local => !EmptyNativeLineScript(actors[line.ObjectId], local)));
            commitContacts = () =>
            {
                foreach (int objectId in contacts)
                {
                    actors[objectId].NativePushContactPending = true;
                }
                CommitNativeLineMovement(lines);
            };
            Scene.NativePlayerMovementVersion = 1;
            Scene.NativePlayerEarlyRetryState = 0;
        }
        else
        {
            player.X = x;
            player.Y = y;
            player.Z = z;
            player.WalkmeshTriangle = triangle;
            player.Direction = direction;
            player.NativeMotionAuthority = new NativeMotionAuthority(1, (byte)direction, 0);
            if (Scene.NativePartyTraceVersion == 1)
            {
                ApplyPartyTraceMovement(player, raw, direction, true, diagnosticTrace);
            }
            else
            {
                Scene.NativePlayerPosition = raw;
            }
        }
        return triggered;
    }

    private void ValidateDiagnosticPartyOwners()
    {
        if (partyJoin is not null || partySplit is not null)
        {
            throw new NotSupportedException("Diagnostic party placement cannot replace an active native join or split owner.");
        }
        for (int slot = 1; slot < 3; slot++)
        {
            ActorState? follower = ResolvePartyActor(slot);
            if (follower is not null && (follower.NativeScriptMovement is { Active: true } || follower.NativeJump is { Active: true } || follower.NativeLadder is { Active: true } || follower.NativeLadderMode3 is { Active: true } || follower.MovementTarget is not null || follower.MovementActorId is not null))
            {
                throw new NotSupportedException("Diagnostic party placement cannot replace an attached follower's independent native movement owner.");
            }
        }
    }

    private PartyTraceState PrepareDiagnosticPartyTrace(FieldFixedPosition position, int triangle, int direction)
    {
        PartyTraceState trace = Scene.PartyTrace ?? throw new NotSupportedException("Diagnostic party placement requires its current native trace.");
        PartyTraceSample? Relocate(PartyTraceSample? sample)
        {
            return sample is null ? null : sample with { X = checked((short)(position.X / 4096)), Y = checked((short)(position.Y / 4096)), Z = checked((short)(position.Z / 4096)), Triangle = triangle, Direction = direction };
        }
        return trace with { FirstTrail = trace.FirstTrail.Select(Relocate).ToArray(), SecondTrail = trace.SecondTrail.Select(Relocate).ToArray() };
    }

    /// <summary>A source interaction can remain active while ordinary player control is enabled.</summary>
    public bool HasPendingPlayerInteraction(int objectId)
    {
        if (program is null || (uint)objectId >= actors.Count)
        {
            return false;
        }
        ActorState actor = actors[objectId];
        EventObject obj = program.Objects[objectId];
        if (actor.NativePushContactPending || actor.DoorLineEntryPending || actor.NativeLineCrossingPending || Scene.NativeDoorPendingEvents?.GetValueOrDefault(objectId) > 0)
        {
            return true;
        }
        foreach (Fiber fiber in fibers.Where(fiber => !fiber.Complete && !fiber.Initialization && fiber.ObjectId == objectId))
        {
            if (obj.IsDoor)
            {
                for (int local = 2; local < Math.Min(8, obj.Scripts.Count); local++)
                {
                    if (fiber.Script.Id == obj.Scripts[local].Id && fiber.NativeDoorSlot == 9 - local)
                    {
                        return true;
                    }
                }
            }
            else if (obj.Scripts.Count > 2 && fiber.Script.Id == obj.Scripts[2].Id && fiber.NativeActorSlot == (obj.IsArea ? 6 : 7))
            {
                return true;
            }
            else if (!obj.IsArea && obj.Scripts.Count > 3 && fiber.Script.Id == obj.Scripts[3].Id && fiber.NativeActorSlot == 6)
            {
                return true;
            }
        }
        return false;
    }
}
