namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Returns the original action query's selected actor without requesting a script.</summary>
    public int? SelectInteractionTarget()
    {
        if (program is null || transitionRequested || services.IsMoviePlaying || !IsUserControlEnabled || IsDialogueBlocking)
        {
            return null;
        }
        if (Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldLoadVersion != 1)
        {
            throw new NotSupportedException("Native interaction requires the completed original field loader and retained actor layout.");
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

    private bool InteractWith(int objectId)
    {
        if (Scene.NativeActorLayoutVersion != 1 || Scene.NativeFieldLoadVersion != 1)
        {
            throw new NotSupportedException("Native interaction requires the completed original field loader and retained actor layout.");
        }
        if (SelectInteractionTarget() != objectId)
        {
            return false;
        }
        if (IsNativeActorRequest(objectId) && Scene.NativeActorObjectIds.Contains(objectId))
        {
            return TalkTo(objectId);
        }
        throw new NotSupportedException("Native interaction requires the shared native actor request scheduler.");
    }
}
