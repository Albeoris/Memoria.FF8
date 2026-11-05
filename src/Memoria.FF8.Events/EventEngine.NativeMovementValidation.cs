using System.Numerics;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateNativeScriptMovements(IReadOnlyList<ActorState> savedActors, SceneEventState savedScene, IReadOnlyList<Fiber> restored, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        ArgumentNullException.ThrowIfNull(program);
        foreach (ActorState actor in savedActors)
        {
            if (actor.NativeMotionAuthority is NativeMotionAuthority authority && (authority.Version != 1 || actor.NativeScriptMovement is { } activeMotion && (activeMotion.Active || activeMotion.Cancelled) && (authority.FacingHold != (NativeMoveFacingHold(activeMotion)) || authority.MovementDirection != activeMotion.MovementDirection)))
            {
                throw new InvalidDataException("Saved native motion authority has an unsupported schema or differs from its active scripted movement scalars.");
            }
            if (actor.NativeScriptMovement is not NativeScriptMovementState motion)
            {
                continue;
            }
            bool ownerExists = bySequence.TryGetValue(motion.FiberSequence, out Fiber? owner);
            bool validOwner = ownerExists && owner!.ObjectId == actor.ObjectId && owner.Script.Id == motion.ScriptId;
            EventScript? issuingScript = program.Objects[actor.ObjectId].Scripts.FirstOrDefault(script => script.Id == motion.ScriptId);
            bool directionPoint = (uint)motion.Operation < program.Operations.Count && program.Operations[motion.Operation].Opcode == Jsm.Opcode.DIRP;
            bool partyTarget = IsNativePartyMove(motion.Operation);
            bool asyncPartyTarget = IsNativeAsyncPartyMove(motion.Operation);
            string movementOpcode = asyncPartyTarget ? "RPMOVEA" : partyTarget ? motion.AutomaticAnimation ? "PMOVEA" : "FMOVEP" : directionPoint ? "DIRP" : motion.Accelerated ? "MACCEL" : motion.HoldsFacing ? motion.Asynchronous ? "RCMOVE" : "CMOVE" : motion.TargetNativeObjectId is not null ? motion.Asynchronous ? "RMOVEA" : motion.AutomaticAnimation ? "MOVEA" : "FMOVEA" : motion.Asynchronous ? motion.AutomaticAnimation ? "RMOVE" : "RFMOVE" : motion.AutomaticAnimation ? "MOVE" : "FMOVE";
            if (asyncPartyTarget && (!motion.Asynchronous || !motion.AutomaticAnimation || motion.HoldsFacing || motion.Accelerated || motion.TargetNativeObjectId is null))
            {
                throw new InvalidDataException("Saved RPMOVEA lost its one-time raw party target or asynchronous native mode1 policy.");
            }
            if (partyTarget && (motion.Asynchronous || !motion.AutomaticAnimation && !IsNativeForcedActorMove(motion.Operation) || motion.HoldsFacing || motion.Accelerated || motion.Active && (!validOwner || owner!.Stack.Count == 0 || !owner.Stack.PeekValue().IsExact || (uint)owner.Stack.Peek() >= 3 || owner.NativeMoveTarget != (int)owner.Stack.Peek())))
            {
                throw new InvalidDataException("Saved PMOVEA lost its full DWORD party-slot consumer or native mode1 policy.");
            }
            if (motion.HoldsFacing && !motion.Accelerated && (motion.AutomaticAnimation || motion.TargetNativeObjectId is not null))
            {
                throw new InvalidDataException("Saved held-facing CMOVE/RCMOVE lost its animation policy.");
            }
            if (motion.ResetAnimationOnCompletion is not null && (motion.ResetAnimationOnCompletion != true || motion.AutomaticAnimation || motion.TargetNativeObjectId is not null && !IsNativeForcedActorMove(motion.Operation)))
            {
                throw new InvalidDataException("Saved forced-animation movement has invalid inherited native animation flags.");
            }
            if (motion.Cancelled && (motion.Active || motion.Suspended || motion.AnimationFlag40000 is null || actor.MovementTarget is not null))
            {
                throw new InvalidDataException("Saved cancelled native movement has invalid mode0 or original asynchronous ownership.");
            }
            if (motion.TargetNativeObjectId is int targetId && ((uint)targetId >= program.NativeObjects.Count || !savedScene.NativeActorObjectIds.Contains(program.NativeObjects[targetId]) || !motion.AutomaticAnimation && !IsNativeForcedActorMove(motion.Operation) || motion.Active && !motion.Asynchronous && !partyTarget && (!validOwner || owner!.Stack.Count == 0 || owner.Stack.Peek() != targetId)))
            {
                throw new InvalidDataException("Saved native MOVEA has invalid original target identity or remaining operand.");
            }
            if (motion.Accelerated && (!motion.HoldsFacing || !motion.AutomaticAnimation || motion.Asynchronous || motion.TargetNativeObjectId is not null || motion.Active && actor.NativeMovementAccelerationTarget is null))
            {
                throw new InvalidDataException("Saved native MACCEL lost its blocking held-facing mode or executed target WORD516.");
            }
            if (motion.Version != 1 || savedScene.NativeActorLayoutVersion != 1 || !savedScene.NativeActorObjectIds.Contains(actor.ObjectId) || (uint)motion.Operation >= program.Operations.Count || program.Operations[motion.Operation].Opcode.ToString() != movementOpcode || motion.Asynchronous && (issuingScript is null || !issuingScript.ContainsOperation(motion.Operation) || ownerExists && !validOwner || validOwner && owner!.WaitReason == WaitReason.NativeScriptMovement || (actor.NativeMotionAuthority is not { Version: 1 } || (motion.Active || motion.Cancelled) && actor.NativeMotionAuthority.FacingHold != NativeMoveFacingHold(motion))) || actor.NativeMovementCompletion542 > 2 || !motion.Active && !motion.Cancelled && actor.NativeMovementCompletion542 == 1 || (directionPoint && (motion.Active && motion.Speed != 0 || motion.StopDistance != 0 || motion.AutomaticAnimation || motion.HoldsFacing || motion.Asynchronous || motion.Accelerated || motion.TargetNativeObjectId is not null)) || motion.Active && motion.Speed != actor.MovementSpeed || motion.Triangle != actor.WalkmeshTriangle || motion.Triangle is < 0 or > short.MaxValue || actor.X != motion.FixedX / 4096f || actor.Y != motion.FixedY / 4096f || actor.Z != motion.FixedZ / 4096f || motion.Active && !motion.Suspended && actor.Direction != motion.FacingDirection || (motion.FixedZ & 4095) != 0 || new[] { motion.FixedX, motion.FixedY, motion.FixedZ, motion.TargetX, motion.TargetY, motion.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || motion.TargetNativeObjectId is null && !partyTarget && new[] { motion.TargetX, motion.TargetY, motion.TargetZ }.Any(value => (value & 4095) != 0) || motion.Active && !motion.Asynchronous && (!validOwner || owner!.Complete || owner.Position != motion.Operation || owner.WaitReason != WaitReason.NativeScriptMovement) || !motion.Active && !motion.Cancelled && validOwner && owner!.WaitReason == WaitReason.NativeScriptMovement || motion.Active && actor.NativeMovementCompletion542 != 2 && actor.MovementTarget != new Vector3(motion.TargetX / 4096f, motion.TargetY / 4096f, motion.TargetZ / 4096f) || actor.NativeMovementCompletion542 == 2 && actor.MovementTarget is not null)
            {
                throw new InvalidDataException("Saved native scripted MOVE has invalid raw placement, owner, instruction wait or completion state.");
            }
        }
        if (restored.Any(fiber => fiber.WaitReason == WaitReason.NativeScriptMovement && fiber.NativeMoveOperation != fiber.Position))
        {
            throw new InvalidDataException("Saved native scripted MOVE wait has no original raw movement state.");
        }
    }
}
