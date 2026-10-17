using System.Numerics;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Explicit diagnostic placement. Contact dispatch uses the current field's normal geometric queries; it never requests a script directly.</summary>
    public bool TeleportPlayer(float x, float y, float z, int triangle, int direction = 0, bool dispatchContacts = false)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue || z is < short.MinValue or > short.MaxValue || triangle is < 0 or > short.MaxValue || direction is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Diagnostic placement requires bounded field coordinates, triangle and direction.");
        }
        if (program is null || transitionRequested || !CanAcceptFieldMovement || HasPendingBattle || PendingTripleTriad is not null || Scene.NativeMenuTutorial is not null || PlayerActor is not ActorState player || NativePlayerHasScriptedMode(player))
        {
            throw new InvalidOperationException("Diagnostic placement requires a free controlled field leader.");
        }
        FieldFixedPosition raw = new(checked((int)MathF.Round(x * 4096)), checked((int)MathF.Round(y * 4096)), checked((int)MathF.Round(z * 4096)));
        x = raw.X / 4096f;
        y = raw.Y / 4096f;
        z = raw.Z / 4096f;
        WalkmeshPosition position = new(new Vector3(x, y, z), triangle);
        if (Scene.NativePartyTraceVersion == 1)
        {
            ReadNativePartyPlayerPosition(player);
            PreparePartyTraceFollowers(position, direction, true);
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
            IReadOnlyList<NativeLineContactChange> lines = MovePlayerCore(x, y, z, triangle, raw, direction, true, raw);
            triggered = transitionRequested || contacts.Length > 0 || actors.Any(actor => actor.DoorLineEntryPending) || lines.Any(line => NativeDoorRequestedLocals(line.PendingEvents, line.Contact).Any(local => !EmptyNativeLineScript(actors[line.ObjectId], local)));
            foreach (int objectId in contacts)
            {
                actors[objectId].NativePushContactPending = true;
            }
            CommitNativeLineMovement(lines);
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
                ApplyPartyTraceMovement(player, raw, direction, true);
            }
            else
            {
                Scene.NativePlayerPosition = raw;
            }
        }
        return triggered;
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
