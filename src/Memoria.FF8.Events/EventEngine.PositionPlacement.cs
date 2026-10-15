namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedNativeSet(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.Stack.Count < 2 || NativeRuntimeProfile is not { IsVerified: true } || program is null || !program.NativeObjects.Take(program.NativeActorCount).Contains(actor.ObjectId))
        {
            return "Native SET requires two original operands and a verified allocated actor body";
        }
        int[] top = fiber.Stack.Take(2).ToArray();
        int x = unchecked(top[1] << 12);
        int y = unchecked(top[0] << 12);
        if ((x >> 12) is < short.MinValue or > short.MaxValue || (y >> 12) is < short.MinValue or > short.MaxValue || ReadNativeSetZ(actor) is null)
        {
            return "Native SET needs bounded shifted DWORD coordinates and preserved original Z";
        }
        if (actor.NativeCopiedPosition is { } copied && actor.Z == copied.FixedZ / 4096f && (copied.FixedZ & 4095) != 0)
        {
            return "Native SET after COPYINFO requires preservation of the copied fractional raw Z";
        }
        if (actor.NativeScriptMovement is { Active: true, Suspended: false } || actor.NativeLadder is { Active: true } || actor.NativeLadderMode3 is { Active: true } || actor.MovementTarget is not null && actor.NativeScriptMovement is not { Suspended: true } || actor.MovementActorId is not null)
        {
            return "Native SET placement during active movement needs its preserved native movement-body replacement contract";
        }
        return null;
    }

    private int? ReadNativeSetZ(ActorState actor)
    {
        if (actor.NativeScriptMovement is NativeScriptMovementState motion && actor.Z == motion.FixedZ / 4096f)
        {
            return motion.FixedZ;
        }
        if (Scene.NativePlayerPosition is FieldFixedPosition player && IsNativePartyPlayerBody(actor) && actor.Z == player.Z / 4096f)
        {
            return player.Z;
        }
        return float.IsFinite(actor.Z) && actor.Z == MathF.Truncate(actor.Z) && actor.Z is >= short.MinValue and <= short.MaxValue ? (int)actor.Z << 12 : null;
    }

    private int[] ExecuteNativeSet(Fiber fiber, int parameter)
    {
        ActorState actor = actors[fiber.ObjectId];
        int z = ReadNativeSetZ(actor)!.Value;
        int[] args = Pop(fiber, 2);
        int x = unchecked(args[0] << 12);
        int y = unchecked(args[1] << 12);
        actor.NativeJoinedPosition = null;
        actor.NativeCopiedPosition = null;
        actor.NativeLadder = null;
        actor.NativeLadderMode3 = null;
        actor.X = x / 4096f;
        actor.Y = y / 4096f;
        actor.WalkmeshTriangle = unchecked((short)parameter);
        if (actor.NativeScriptMovement is NativeScriptMovementState completed)
        {
            // SET changes body placement, not the stored completed motion's target or body direction.
            actor.NativeScriptMovement = completed with { FixedX = x, FixedY = y, FixedZ = z, Triangle = actor.WalkmeshTriangle };
        }
        if (Scene.NativePartyTraceVersion == 1 && IsNativePartyPlayerBody(actor))
        {
            Scene.NativePlayerPosition = new FieldFixedPosition(x, y, z);
        }
        // 51D720→477890 clears every door+406/current-contact and+413/button-facing
        // byte. Pending entry/facing/crossing/exit events are distinct and retained.
        ClearNativeDoorCurrentContacts();
        // Native return2 continues the same instruction budget; no wait or height lookup.
        return args;
    }

    private void ClearNativeDoorCurrentContacts()
    {
        foreach (ActorState door in actors.Where(item => program!.Objects[item.ObjectId].IsDoor))
        {
            door.LineContact = false;
            door.NativeLineFacingContact = false;
        }
    }
}
