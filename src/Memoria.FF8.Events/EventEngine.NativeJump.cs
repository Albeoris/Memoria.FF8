using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

/// <summary>Native mode572=2, WORD472/474/542/508 and DWORD400..448, preserved independently of grounded MOVE.</summary>
public sealed record NativeJumpState(int Version, bool Active, long FiberSequence, int ScriptId, int Operation, int FixedX, int FixedY, int FixedZ, int Triangle, int StartX, int StartY, int StartZ, int TargetX, int TargetY, int TargetZ, int TargetTriangle, short Duration, short Elapsed, int Velocity, byte Completion);

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
    private string? UnsupportedNativeJump(Fiber fiber, int triangle)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (actor.NativeJump is { Active: true } current)
        {
            return current.FiberSequence == fiber.Sequence && current.ScriptId == fiber.Script.Id && current.Operation == fiber.Position && current.TargetTriangle == unchecked((ushort)triangle) && MatchesNativeJumpPlacement(actor, current) ? null : "Native JUMP requires its original held owner and raw mode2 placement";
        }
        if (Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || fiber.Stack.Count < 3 || actor.NativeScriptMovement is { Active: true } || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.RotationTarget is not null || ReadNativeScriptPosition(actor) is null || ResolveNativeTriangleHeight is null)
        {
            return "Native JUMP requires three original operands, a retained idle body, preserved fixed placement and the original explicit-triangle plane";
        }
        int[] operands = fiber.Stack.Take(3).Reverse().ToArray();
        short duration = unchecked((short)operands[2]);
        int x = unchecked(operands[0] << 12);
        int y = unchecked(operands[1] << 12);
        if (duration <= 0 || (x >> 12) is < short.MinValue or > short.MaxValue || (y >> 12) is < short.MinValue or > short.MaxValue || ResolveNativeTriangleHeight(unchecked((ushort)triangle), x / 4096, y / 4096) is not int height || height is < short.MinValue or > short.MaxValue)
        {
            return "Native JUMP requires a positive signed duration and a certified original landing plane; zero duration divides by zero in the native driver";
        }
        return null;
    }

    private int[] ExecuteNativeJump(Fiber fiber, int triangle)
    {
        ActorState actor = actors[fiber.ObjectId];
        int[] operands = [];
        if (actor.NativeJump is not { Active: true })
        {
            FieldFixedPosition raw = ReadNativeScriptPosition(actor)!;
            operands = Pop(fiber, 3);
            actor.NativeScriptMovement = null;
            actor.NativeJump = new NativeJumpState(1, true, fiber.Sequence, fiber.Script.Id, fiber.Position - 1, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, raw.X, raw.Y, raw.Z, unchecked(operands[0] << 12), unchecked(operands[1] << 12), 0, unchecked((ushort)triangle), unchecked((short)operands[2]), 0, 0, 0);
        }
        else if (actor.NativeJump.Completion == 2)
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
            if (current.Completion == 0)
            {
                int? height = ResolveNativeTriangleHeight?.Invoke(current.TargetTriangle, current.TargetX / 4096, current.TargetY / 4096);
                if (height is null || height is < short.MinValue or > short.MaxValue)
                {
                    string message = "Native mode2 JUMP cannot resolve its original explicit landing plane; raw body preserved.";
                    LastFault = new EventFault(FieldId, objectId, current.ScriptId, current.Operation, "JUMP", message);
                    throw new EventScriptException(message);
                }
                int targetZ = unchecked(height.Value << 12);
                next = current with { StartX = current.FixedX, StartY = current.FixedY, StartZ = current.FixedZ, TargetZ = targetZ, Elapsed = 0, Velocity = NativeJumpMath.Velocity(current.FixedZ, targetZ, current.Duration), Completion = 1 };
            }
            else if (current.Elapsed == current.Duration)
            {
                // Native completion copies triangle508 to506 one update after the final position; no endpoint snap.
                next = current with { Triangle = unchecked((short)current.TargetTriangle), Completion = 2 };
            }
            else
            {
                short elapsed = unchecked((short)(current.Elapsed + 1));
                next = current with { FixedX = NativeJumpMath.Interpolate(current.StartX, current.TargetX, current.Duration, elapsed), FixedY = NativeJumpMath.Interpolate(current.StartY, current.TargetY, current.Duration, elapsed), FixedZ = NativeJumpMath.Height(current.StartZ, current.Velocity, elapsed), Elapsed = elapsed };
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
            bool boundedTarget = jump.TargetTriangle is >= 0 and <= ushort.MaxValue && (jump.TargetX >> 12) is >= short.MinValue and <= short.MaxValue && (jump.TargetY >> 12) is >= short.MinValue and <= short.MaxValue && (jump.TargetX & 4095) == 0 && (jump.TargetY & 4095) == 0;
            int? landingHeight = boundedTarget ? ResolveNativeTriangleHeight?.Invoke(jump.TargetTriangle, jump.TargetX / 4096, jump.TargetY / 4096) : null;
            bool invalid = jump.Version != 1 || saved.Scene.NativeActorLayoutVersion != 1 || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || jump.Duration <= 0 || jump.Elapsed < 0 || jump.Elapsed > jump.Duration || jump.Completion is < 1 or > 2 || !jump.Active && jump.Completion != 2 || jump.Completion == 2 && jump.Elapsed != jump.Duration || issuingScript is null || jump.Operation < issuingScript.Start || jump.Operation >= issuingScript.End || (uint)jump.Operation >= program.Operations.Count || program.Operations[jump.Operation].Opcode != Jsm.Opcode.JUMP || unchecked((ushort)program.Operations[jump.Operation].Parameter) != jump.TargetTriangle || !boundedTarget || landingHeight is null || landingHeight is < short.MinValue or > short.MaxValue || jump.TargetZ != unchecked(landingHeight.Value << 12) || owner is not null && (owner.ObjectId != actor.ObjectId || owner.Script.Id != jump.ScriptId) || actor.NativeScriptMovement is { Active: true } || actor.MovementTarget is not null || !MatchesNativeJumpPlacement(actor, jump) || jump.Velocity != NativeJumpMath.Velocity(jump.StartZ, jump.TargetZ, jump.Duration) || jump.FixedX != NativeJumpMath.Interpolate(jump.StartX, jump.TargetX, jump.Duration, jump.Elapsed) || jump.FixedY != NativeJumpMath.Interpolate(jump.StartY, jump.TargetY, jump.Duration, jump.Elapsed) || jump.FixedZ != NativeJumpMath.Height(jump.StartZ, jump.Velocity, jump.Elapsed) || (jump.TargetZ & 4095) != 0 || jump.Active && (owner is null || owner.Complete || owner.Position != jump.Operation || owner.WaitReason != WaitReason.NativeJump);
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
