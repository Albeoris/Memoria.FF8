using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Native mode572=2, WORD472/474/542/508 and DWORD400..448, preserved independently of grounded MOVE.</summary>
public sealed record NativeJumpState(int Version, bool Active, long FiberSequence, int ScriptId, int Operation, int FixedX, int FixedY, int FixedZ, int Triangle, int StartX, int StartY, int StartZ, int TargetX, int TargetY, int TargetZ, short Duration, int Velocity, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? AsyncFlag20000 = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? AnimationFlag40000 = null, [property: JsonRequired] int? LandingPlaneTriangle = null);

public static class NativeJumpMath
{
    public static int Interpolate(int start, int target, short duration, short elapsed)
    {
        // Native475C60 changes division order at +/-0x80000 to avoid the small-delta multiply overflow.
        int delta = unchecked(target - start);
        return duration == 0 ? start : unchecked(start + (delta > -524288 && delta < 524288 ? unchecked(elapsed * delta) / duration : unchecked(elapsed * (delta / duration))));
    }

    public static int Velocity(int startZ, int targetZ, short duration)
    {
        return unchecked(unchecked(16000 * duration) / 2 + unchecked(targetZ - startZ) / duration);
    }

    public static int Height(int startZ, int velocity, short elapsed)
    {
        return unchecked(startZ + unchecked(elapsed * velocity) - unchecked(16000 * elapsed * elapsed) / 2);
    }
}

public sealed partial class EventEngine
{
    private string? UnsupportedNativeJump(Fiber fiber, int triangle, bool includesZ = false)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (actor.NativeMovementCompletion542 is null || actor.NativeLandingTriangle508 is null || actor.NativeTurnAccumulator474 is null)
        {
            return "Native JUMP requires allocated physical WORD474/508/542";
        }
        if (actor.NativeJump is { Active: true } current)
        {
            return current.FiberSequence == fiber.Sequence && current.ScriptId == fiber.Script.Id && current.Operation == fiber.Position && MatchesNativeJumpPlacement(actor, current) ? null : "Native JUMP requires its original held owner and raw mode2 placement";
        }
        int operandCount = includesZ ? 4 : 3;
        bool replacingRequest = IsNativeMovementRequestIssue(actor, fiber);
        if (Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || fiber.Stack.Count < operandCount || !replacingRequest && (actor.NativeScriptMovement is { Active: true } && actor.NativeMovementCompletion542 < 2 || actor.MovementTarget is not null) || actor.MovementActorId is not null || actor.RotationTarget is not null || ReadNativeScriptPosition(actor) is null || ResolveNativeTriangleHeight is null)
        {
            return $"Native JUMP requires {operandCount} original operands, a retained idle body, preserved fixed placement and the original explicit-triangle plane";
        }
        int[] operands = fiber.Stack.Take(operandCount).Reverse().ToArray();
        short duration = unchecked((short)operands[^1]);
        int x = unchecked(operands[0] << 12);
        int y = unchecked(operands[1] << 12);
        if (duration <= 0 || (x >> 12) is < short.MinValue or > short.MaxValue || (y >> 12) is < short.MinValue or > short.MaxValue || ResolveNativeTriangleHeight(unchecked((ushort)triangle), x / 4096, y / 4096) is not int height || height is < short.MinValue or > short.MaxValue)
        {
            return "Native JUMP requires a positive signed duration and a certified original landing plane; zero duration divides by zero in the native driver";
        }
        return null;
    }

    private int[] ExecuteNativeJump(Fiber fiber, int triangle, bool includesZ = false)
    {
        ActorState actor = actors[fiber.ObjectId];
        int[] operands = [];
        if (actor.NativeJump is not { Active: true })
        {
            FieldFixedPosition raw = ReadNativeScriptPosition(actor)!;
            operands = Pop(fiber, includesZ ? 4 : 3);
            NativeScriptMovementState? previous = actor.NativeScriptMovement;
            bool? asyncFlag = previous is null ? actor.NativeJump?.AsyncFlag20000 : previous.Active && previous.Asynchronous;
            bool? animationFlag = previous is null ? actor.NativeJump?.AnimationFlag40000 : previous.AnimationFlag40000 ?? (previous.Suspended ? previous.ResetAnimationOnCompletion ?? previous.AutomaticAnimation : previous.Active && (previous.ResetAnimationOnCompletion ?? previous.AutomaticAnimation));
            actor.NativeScriptMovement = null;
            actor.NativeLadder = null;
            actor.NativeMovementCompletion542 = 0;
            actor.NativeLandingTriangle508 = unchecked((ushort)triangle);
            actor.NativeJoinedPosition = null;
            actor.MovementTarget = null;
            // JUMP3's supplied target444 is overwritten by the same mode2 plane evaluation before velocity is calculated.
            actor.NativeJump = new NativeJumpState(1, true, fiber.Sequence, fiber.Script.Id, fiber.Position - 1, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, raw.X, raw.Y, raw.Z, unchecked(operands[0] << 12), unchecked(operands[1] << 12), includesZ ? unchecked(operands[2] << 12) : 0, unchecked((short)operands[^1]), 0, asyncFlag, animationFlag);
        }
        else if (actor.NativeMovementCompletion542 == 2)
        {
            actor.NativeJump = actor.NativeJump with { Active = false };
            // Native5256A0 returns3: advance this instruction and yield the actor's VM budget.
            WaitFrames(fiber, 1);
            return operands;
        }
        fiber.Position--;
        fiber.WaitReason = WaitReason.NativeJump;
        return operands;
    }

    private void CompleteNativeJumpAsyncMovement(ActorState actor)
    {
        if (actor.NativeMovementCompletion542 != 2 || actor.NativeJump is not { AsyncFlag20000: true } jump)
        {
            return;
        }
        // JUMP overwrites572/542, retaining prior flags20000/40000 until529FF0's next completion consumer.
        if (jump.AnimationFlag40000 == true)
        {
            SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
        }
        actor.NativeJump = jump with { AsyncFlag20000 = false, AnimationFlag40000 = false };
        if (actor.NativeMotionAuthority is { } authority)
        {
            actor.NativeMotionAuthority = authority with { FacingHold = 0 };
        }
    }

    private void StepNativeJump()
    {
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState actor = actors[objectId];
            if (actor.NativeJump is not { Active: true } current)
            {
                continue;
            }
            NativeJumpState next;
            if (actor.NativeMovementCompletion542 == 0)
            {
                int? height = ResolveNativeTriangleHeight?.Invoke(actor.NativeLandingTriangle508!.Value, current.TargetX / 4096, current.TargetY / 4096);
                if (height is null || height is < short.MinValue or > short.MaxValue)
                {
                    string opcode = program!.Operations[current.Operation].Opcode.ToString();
                    string message = $"Native mode2 {opcode} cannot resolve its original explicit landing plane; raw body preserved.";
                    LastFault = new EventFault(FieldId, objectId, current.ScriptId, current.Operation, opcode, message);
                    throw new EventScriptException(message);
                }
                int targetZ = unchecked(height.Value << 12);
                next = current with { StartX = current.FixedX, StartY = current.FixedY, StartZ = current.FixedZ, TargetZ = targetZ, LandingPlaneTriangle = actor.NativeLandingTriangle508!.Value, Velocity = NativeJumpMath.Velocity(current.FixedZ, targetZ, current.Duration) };
                actor.NativeMovementCompletion542 = 1;
                actor.NativeTurnAccumulator474 = 0;
            }
            else if (actor.NativeTurnAccumulator474 == current.Duration)
            {
                // Native completion copies triangle508 to506 one update after the final position; no endpoint snap.
                next = current with { Triangle = unchecked((short)actor.NativeLandingTriangle508!.Value) };
                actor.NativeMovementCompletion542 = 2;
            }
            else
            {
                short elapsed = unchecked((short)(actor.NativeTurnAccumulator474!.Value + 1));
                next = current with { FixedX = NativeJumpMath.Interpolate(current.StartX, current.TargetX, current.Duration, elapsed), FixedY = NativeJumpMath.Interpolate(current.StartY, current.TargetY, current.Duration, elapsed), FixedZ = NativeJumpMath.Height(current.StartZ, current.Velocity, elapsed) };
                actor.NativeTurnAccumulator474 = elapsed;
            }
            actor.NativeJump = next;
            actor.X = next.FixedX / 4096f;
            actor.Y = next.FixedY / 4096f;
            actor.Z = next.FixedZ / 4096f;
            actor.WalkmeshTriangle = next.Triangle;
            if (Scene.NativePartyTraceVersion == 1 && IsNativePartyPlayerBody(actor))
            {
                Scene.NativePlayerPosition = new FieldFixedPosition(next.FixedX, next.FixedY, next.FixedZ);
                ClearNativeDoorCurrentContacts();
            }
        }
    }

    private static bool MatchesNativeJumpPlacement(ActorState actor, NativeJumpState jump)
    {
        return actor.X == jump.FixedX / 4096f && actor.Y == jump.FixedY / 4096f && actor.Z == jump.FixedZ / 4096f && actor.WalkmeshTriangle == jump.Triangle;
    }

    private void ValidateNativeJumps(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        foreach (ActorState actor in saved.Actors)
        {
            if (actor.NativeJump is not NativeJumpState jump)
            {
                continue;
            }
            Fiber? owner = restored.SingleOrDefault(fiber => fiber.Sequence == jump.FiberSequence);
            EventScript? issuingScript = (uint)actor.ObjectId < program!.Objects.Count ? program.Objects[actor.ObjectId].Scripts.SingleOrDefault(script => script.Id == jump.ScriptId) : null;
            bool boundedTarget = actor.NativeLandingTriangle508 is not null && (jump.TargetX >> 12) is >= short.MinValue and <= short.MaxValue && (jump.TargetY >> 12) is >= short.MinValue and <= short.MaxValue && (jump.TargetX & 4095) == 0 && (jump.TargetY & 4095) == 0;
            int? landingHeight = jump.LandingPlaneTriangle is >= 0 and <= ushort.MaxValue ? ResolveNativeTriangleHeight?.Invoke(jump.LandingPlaneTriangle.Value, jump.TargetX / 4096, jump.TargetY / 4096) : null;
            if (jump.AsyncFlag20000 == true && (jump.AnimationFlag40000 is null || actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 }) || actor.NativeMovementRequest is { } request && (request.RequestSequence != jump.FiberSequence || request.RequestScriptId != jump.ScriptId || jump.AsyncFlag20000 is null || jump.AnimationFlag40000 is null))
            {
                throw new InvalidDataException("Saved native JUMP lost its inherited physical motion flags or the higher request slot's original owner.");
            }
            bool invalid = jump.Version != 1 || saved.Scene.NativeActorLayoutVersion != 1 || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || jump.Duration <= 0 || actor.NativeTurnAccumulator474 is null or < 0 || actor.NativeTurnAccumulator474 > jump.Duration || actor.NativeMovementCompletion542 is null or > 2 || issuingScript is null || jump.Operation < issuingScript.Start || jump.Operation >= issuingScript.End || (uint)jump.Operation >= program.Operations.Count || program.Operations[jump.Operation].Opcode is not (Jsm.Opcode.JUMP or Jsm.Opcode.JUMP3) || !boundedTarget || landingHeight is null or < short.MinValue or > short.MaxValue || jump.TargetZ != unchecked(landingHeight.Value << 12) || owner is not null && (owner.ObjectId != actor.ObjectId || owner.Script.Id != jump.ScriptId) || actor.NativeScriptMovement is { Active: true } || actor.MovementTarget is not null || !MatchesNativeJumpPlacement(actor, jump) || jump.Velocity != NativeJumpMath.Velocity(jump.StartZ, jump.TargetZ, jump.Duration) || jump.FixedX != NativeJumpMath.Interpolate(jump.StartX, jump.TargetX, jump.Duration, actor.NativeTurnAccumulator474!.Value) || jump.FixedY != NativeJumpMath.Interpolate(jump.StartY, jump.TargetY, jump.Duration, actor.NativeTurnAccumulator474!.Value) || jump.FixedZ != NativeJumpMath.Height(jump.StartZ, jump.Velocity, actor.NativeTurnAccumulator474!.Value) || (jump.TargetZ & 4095) != 0 || jump.Active && (owner is null || owner.Complete || owner.Position != jump.Operation || owner.WaitReason != WaitReason.NativeJump);
            if (invalid)
            {
                throw new InvalidDataException("Saved native JUMP has inconsistent raw body, ballistic phase, landing plane or held original instruction.");
            }
        }
        if (restored.Any(fiber => fiber.WaitReason == WaitReason.NativeJump && saved.Actors[fiber.ObjectId].NativeJump is not { Active: true }))
        {
            throw new InvalidDataException("Saved native JUMP wait has no preserved mode2 body.");
        }
    }
}
