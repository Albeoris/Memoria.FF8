namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedPartyLinearTurn(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        NativeActorFacingQuery? query = PreparePartyActorFacing(fiber, 1);
        return fiber.Stack.Count >= 2 && query is not null && ResolveNativeActorFacing?.Invoke(query) is not null && float.IsFinite(actor.Direction) && actor.Direction == (byte)actor.Direction && actor.RotationTarget is null ? null : "PLTURN requires two original DWORD operands, known saved compact party binding, certified raw actor facing and an idle exact BYTE577 direction";
    }

    private int[] ExecutePartyLinearTurn(Fiber fiber)
    {
        NativeActorFacingQuery query = PreparePartyActorFacing(fiber, 1)!;
        byte target = ResolveNativeActorFacing!(query)!.Value;
        int[] operands = Pop(fiber, 2);
        ActorState actor = actors[fiber.ObjectId];
        int start = (byte)actor.Direction;
        actor.RotationStart = start;
        actor.RotationTotalFrames = unchecked((byte)operands[1]);
        actor.RotationTarget = start == target ? null : NearestFaceAngle(start, target);
        actor.RotationUsesNativeCosine = false;
        actor.RotationUsesNativeLinear = actor.RotationTarget is not null;
        actor.RotationFrames = actor.RotationUsesNativeLinear ? actor.RotationTotalFrames + 1 : 0;
        // Native527790 returns2 immediately for an equal direction; otherwise mode1 samples elapsed0..duration.
        fiber.WaitReason = actor.RotationTarget is null ? WaitReason.None : WaitReason.Rotation;
        return operands;
    }

    private static void ValidateNativeLinearTurns(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors.Where(actor => actor.RotationUsesNativeLinear))
        {
            int elapsed = Math.Max(0, actor.RotationTotalFrames - actor.RotationFrames);
            if (actor.RotationUsesNativeCosine || actor.RotationStart is < 0 or > 255 || actor.RotationStart != (byte)actor.RotationStart || actor.RotationTarget is not float target || target is < -128 or > 383 || target != MathF.Truncate(target) || actor.RotationTotalFrames is < 0 or > 255 || actor.RotationFrames < 1 || actor.RotationFrames > actor.RotationTotalFrames + 1 || actor.Direction != unchecked((byte)NativeJumpMath.Interpolate((int)actor.RotationStart, (int)target, (short)actor.RotationTotalFrames, (short)elapsed)) && !NativeMovementOwnsFacing(actor))
            {
                throw new InvalidDataException("Saved native linear turn has inconsistent signed target, BYTE duration, elapsed phase or visible direction.");
            }
        }
    }
}
