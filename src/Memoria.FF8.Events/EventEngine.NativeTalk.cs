namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private const string Native129TalkProgramHash = "CBF3122BECCE857482F93D49D96ECAC41B57B8A42776F9426779AC15EEA5D7E6";

    /// <summary>Returns the original action query's selected actor without requesting a script.</summary>
    public int? SelectInteractionTarget()
    {
        if (program is null || transitionRequested || services.IsMoviePlaying || !IsUserControlEnabled || IsDialogueBlocking)
        {
            return null;
        }
        if (Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldLoadVersion != 1)
        {
            ActorState? legacyPlayer = actors.FirstOrDefault(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
            return legacyPlayer is null ? null : actors.Where(actor => actor.ObjectId != legacyPlayer.ObjectId && actor.Active && actor.Visible && actor.TalkEnabled && LegacyInteractionInRange(legacyPlayer, actor)).OrderBy(actor => Math.Pow(actor.X - legacyPlayer.X, 2) + Math.Pow(actor.Y - legacyPlayer.Y, 2)).Select(actor => (int?)actor.ObjectId).FirstOrDefault();
        }
        ActorState? player = ResolvePartyActor(0);
        if (player is null || player.ModelId is < 0 or ushort.MaxValue || player.Direction != MathF.Truncate(player.Direction) || player.Direction is < 0 or > 255)
        {
            return null;
        }
        int x = (int)MathF.Floor(player.X);
        int y = (int)MathF.Floor(player.Y);
        int z = (int)MathF.Floor(player.Z);
        int bestAngle = 64;
        int? selected = null;
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState target = actors[objectId];
            // Native4796E0 visits the retained model array, including hidden actors.
            if (target.ObjectId == player.ObjectId || !target.TalkEnabled || target.ModelId is < 0 or ushort.MaxValue)
            {
                continue;
            }
            int tx = (int)MathF.Floor(target.X);
            int ty = (int)MathF.Floor(target.Y);
            int tz = (int)MathF.Floor(target.Z);
            int dz = z - tz;
            long dx = (long)tx - x;
            long dy = (long)ty - y;
            long squared = dx * dx + dy * dy;
            if (squared == 0 || squared > int.MaxValue || dz <= -256 || dz >= 256)
            {
                continue;
            }
            int distance = (int)Math.Sqrt(squared);
            int radius = (ushort)player.PushRadius + (ushort)target.TalkRadius;
            if (distance >= radius)
            {
                continue;
            }
            int direction = NativePartyDirection(x, y, tx, ty);
            int angle = (byte)((byte)player.Direction - direction);
            if (angle > 128)
            {
                angle = 256 - angle;
            }
            if (angle < bestAngle)
            {
                bestAngle = angle;
                selected = objectId;
            }
        }
        return selected;
    }

    private static bool LegacyInteractionInRange(ActorState player, ActorState target)
    {
        float dx = target.X - player.X;
        float dy = target.Y - player.Y;
        int radius = Math.Max(0, target.TalkRadius);
        return dx * dx + dy * dy <= (long)radius * radius;
    }

    private bool InteractWith(int objectId)
    {
        if (Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldLoadVersion != 1)
        {
            ActorState? target = actors.FirstOrDefault(actor => actor.ObjectId == objectId);
            ActorState? player = actors.FirstOrDefault(actor => actor.Active && actor.Visible && actor.CharacterId == 0);
            return target is not null && player is not null && LegacyInteractionInRange(player, target) && TalkTo(objectId);
        }
        if (SelectInteractionTarget() != objectId)
        {
            return false;
        }
        if (IsNativeActorRequest(objectId) && Scene.NativeActorObjectIds.Contains(objectId))
        {
            return TalkTo(objectId);
        }
        // Scoped direct native slot7 translation. Other fields retain their
        // bounded legacy script scheduling until their action dispatch is proved.
        if (FieldId == 129 && programHash == Native129TalkProgramHash && objectId is 10 or 11)
        {
            EventObject target = program!.Objects[objectId];
            if (target.Scripts.Count < 3 || fibers.Any(fiber => fiber.ObjectId == objectId && !fiber.Complete) && !CanPreemptNativeHalt(actors[objectId]))
            {
                throw new NotSupportedException("Native guard action requires its original local2 and an idle target; competing requests preserve original state.");
            }
            if (NativeRuntimeProfile is { IsVerified: true })
            {
                Fiber action = Queue(objectId, target.Scripts[2].Id, 7, false);
                action.NativeActorSlot = 7;
            }
            else
            {
                Queue(objectId, target.Scripts[2].Id, 0, false);
            }
            return true;
        }
        return TalkTo(objectId);
    }
}
