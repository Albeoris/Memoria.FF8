namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private string? UnsupportedPartyTurn(Fiber fiber, bool cosine, bool asynchronous = false)
    {
        string opcode = cosine ? "PCTURN" : "PLTURN";
        if (fiber.Stack.Count < 2 || fiber.Stack.Capture().Take(2).Any(value => !value.IsExact))
        {
            return $"{opcode} requires its two original exact DWORD operands";
        }
        ActorState actor = actors[fiber.ObjectId];
        NativeActorFacingQuery? query = PreparePartyActorFacing(fiber, 1);
        return query is not null && ResolveNativeActorFacing?.Invoke(query) is not null && float.IsFinite(actor.Direction) && actor.Direction == (byte)actor.Direction && (asynchronous || actor.RotationTarget is null) && (!asynchronous || actor.NativeTurnMode580 is not null) ? null : $"{opcode} requires its known saved compact party binding, certified raw actor facing and exact native turn state; unowned native bodies are unsupported";
    }

    private int[] ExecutePartyTurn(Fiber fiber, bool cosine, bool asynchronous = false)
    {
        NativeActorFacingQuery query = PreparePartyActorFacing(fiber, 1)!;
        byte target = ResolveNativeActorFacing!(query)!.Value;
        int[] operands = Pop(fiber, 2);
        ActorState actor = actors[fiber.ObjectId];
        int start = (byte)actor.Direction;
        actor.RotationStart = start;
        actor.RotationTotalFrames = unchecked((byte)operands[1]);
        actor.RotationTarget = start == target ? null : NearestFaceAngle(start, target);
        actor.RotationUsesNativeCosine = cosine && actor.RotationTarget is not null;
        actor.RotationUsesNativeLinear = !cosine && actor.RotationTarget is not null;
        actor.RotationFrames = actor.RotationTarget is not null ? actor.RotationTotalFrames + 1 : 0;
        if (actor.RotationTarget is not null)
        {
            actor.NativeTurnMode580 = cosine ? (byte)2 : (byte)1;
        }
        else if (asynchronous)
        {
            actor.NativeTurnMode580 = 3;
        }
        //527790/5278A0 use saved BYTE194+slot, including after FOLLOWOFF.
        //Equal direction returns2 immediately; mode1/2 samples elapsed0..duration.
        fiber.WaitReason = asynchronous || actor.RotationTarget is null ? WaitReason.None : WaitReason.Rotation;
        return operands;
    }

    private static void ValidateNativeLinearTurns(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors.Where(actor => actor.RotationUsesNativeLinear))
        {
            int elapsed = Math.Max(0, actor.RotationTotalFrames - actor.RotationFrames);
            if (actor.RotationUsesNativeCosine || actor.RotationStart is < 0 or > 255 || actor.RotationStart != (byte)actor.RotationStart || actor.RotationTarget is not float target || target is < short.MinValue or > short.MaxValue || target != MathF.Truncate(target) || actor.RotationTotalFrames is < 0 or > 255 || actor.RotationFrames < 1 || actor.RotationFrames > actor.RotationTotalFrames + 1 || actor.Direction != unchecked((byte)NativeJumpMath.Interpolate((int)actor.RotationStart, (int)target, (short)actor.RotationTotalFrames, (short)elapsed)) && !NativeMovementOwnsFacing(actor))
            {
                throw new InvalidDataException("Saved native linear turn has inconsistent signed target, BYTE duration, elapsed phase or visible direction.");
            }
        }
    }
}
