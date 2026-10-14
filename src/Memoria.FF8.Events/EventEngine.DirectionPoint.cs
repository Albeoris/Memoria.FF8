using System.Numerics;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool IsNativeDirectionPoint(NativeScriptMovementState movement) => program is not null && (uint)movement.Operation < program.Operations.Count && program.Operations[movement.Operation].Opcode == Jsm.Opcode.DIRP;

    private NativeScriptMovementState CreateNativeDirectionPoint(Fiber fiber, int operation)
    {
        ActorState actor = actors[fiber.ObjectId];
        FieldFixedPosition raw = ReadNativeScriptPosition(actor) ?? throw new NotSupportedException("DIRP requires its current raw body position.");
        int[] args = fiber.Stack.Take(3).Reverse().ToArray();
        NativeMotionAuthority authority = actor.NativeMotionAuthority!;
        bool? animation = actor.NativeScriptMovement is { } prior && (prior.AnimationFlag40000 ?? prior.ResetAnimationOnCompletion ?? prior.Active && prior.AutomaticAnimation) ? true : null;
        return new NativeScriptMovementState(1, true, fiber.Sequence, fiber.Script.Id, operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, unchecked(args[0] << 12), unchecked(args[1] << 12), unchecked(args[2] << 12), 0, 0, authority.MovementDirection, (byte)actor.Direction, AutomaticAnimation: false, AnimationFlag40000: animation, FacingHold576: authority.FacingHold == 0 ? null : authority.FacingHold);
    }

    private string? UnsupportedNativeDirectionPoint(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (NativeRuntimeProfile is not { IsVerified: true } || actor.NativeMovementCompletion542 is null || actor.NativeTurnAccumulator474 is null || actor.NativeMovementBaseSpeed is null or <= 0 || actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || ReadNativeScriptPosition(actor) is null || !float.IsFinite(actor.Direction) || actor.Direction != (byte)actor.Direction || actor.PushRadius is < 0 or > ushort.MaxValue)
        {
            return "DIRP requires its allocated WORD474/512/542, current motion bytes and certified zero-speed native body adapters";
        }
        if (fiber.NativeMoveOperation is int issued)
        {
            return issued == fiber.Position && actor.NativeScriptMovement is { Suspended: false } ? null : "DIRP retry requires its issued VM bit and actual shared mode1 body";
        }
        if (fiber.Stack.Count < 3 || actor.MovementActorId is not null || partyJoin is not null || partySplit is not null || actor.NativeScriptMovement is { Active: true, Asynchronous: true, Suspended: false } || actor.RotationTarget is not null && !actor.RotationUsesNativeCosine && !actor.RotationUsesNativeLinear)
        {
            return "DIRP requires three XYZ operands without an unsupported competing body or asynchronous completion flag";
        }
        try
        {
            NativeScriptMovementState movement = CreateNativeDirectionPoint(fiber, fiber.Position);
            if (new[] { movement.TargetX, movement.TargetY, movement.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
            {
                return "DIRP wrapped DWORD target exceeds the current native ground coordinate contract";
            }
            _ = PrepareNativeScriptStep(actor, movement, issuing: true);
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativeDirectionPoint(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (fiber.NativeMoveOperation is null)
        {
            NativeScriptMovementState movement = CreateNativeDirectionPoint(fiber, fiber.Position - 1);
            int[] args = Pop(fiber, 3);
            actor.NativeScriptMovement = movement;
            actor.NativeMovementCompletion542 = 0;
            actor.MovementSpeed = 0;
            actor.MovementStopDistance = 0;
            actor.MovementFlags = 0;
            actor.MovementTarget = new Vector3(movement.TargetX / 4096f, movement.TargetY / 4096f, movement.TargetZ / 4096f);
            actor.MovementIgnoresWalkmesh = false;
            actor.NativeJoinedPosition = null;
            actor.NativeSplitPosition = null;
            actor.NativeJump = null;
            //526E90 returns1: operands are consumed once, and the body runs after this VM budget.
            fiber.Position--;
            fiber.NativeMoveOperation = fiber.Position;
            fiber.NativeMoveTarget = null;
            fiber.WaitReason = WaitReason.NativeScriptMovement;
            return args;
        }
        //The handler's retry does not poll542: it clears572, restores512→510 and explicitly writes542=2.
        NativeScriptMovementState current = actor.NativeScriptMovement!;
        short speed = actor.NativeMovementBaseSpeed!.Value;
        actor.NativeScriptMovement = current with { Active = false, Suspended = false, Cancelled = false, FiberSequence = fiber.Sequence, ScriptId = fiber.Script.Id, Operation = fiber.Position - 1, Speed = speed, Asynchronous = false, AutomaticAnimation = false, TargetNativeObjectId = null, Accelerated = false, HoldsFacing = false, FacingHold576 = actor.NativeMotionAuthority!.FacingHold == 0 ? null : actor.NativeMotionAuthority.FacingHold };
        actor.MovementSpeed = speed;
        actor.NativeMovementCompletion542 = 2;
        actor.MovementTarget = null;
        fiber.NativeMoveOperation = null;
        fiber.NativeMoveTarget = null;
        fiber.WaitReason = WaitReason.None;
        return [];
    }
}
