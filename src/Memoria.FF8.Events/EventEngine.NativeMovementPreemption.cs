using System.Numerics;
using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeMovementRequestState(int Version, long RequestSequence, int RequestScriptId, byte RequestSlot, NativeScriptMovementState SavedMovement);

public sealed partial class EventEngine
{
    private int NativeActorRequestSlot(Fiber fiber) => fiber.NativeDoorSlot ?? fiber.NativeActorSlot ?? (fiber.Priority == 7 && program!.Objects[fiber.ObjectId].Scripts.Count > 1 && fiber.Script.Id == program.Objects[fiber.ObjectId].Scripts[1].Id ? 0 : fiber.Priority);

    private bool IsNativeActorRequest(int objectId) => NativeRuntimeProfile is { IsVerified: true } && Scene.NativeActorLayoutVersion == 1 && program!.NativeObjects.Contains(objectId) && !program.Objects[objectId].IsDoor && (Scene.NativeActorObjectIds.Contains(objectId) || program.Objects[objectId].BackgroundModuleIndex is not null || program.Objects[objectId].IsArea);

    private bool RequestPriorityOccupied(int objectId, int priority) => fibers.Any(fiber => fiber.ObjectId == objectId && !fiber.Complete && (IsNativeActorRequest(objectId) || IsNativeDoorRequest(objectId) ? NativeActorRequestSlot(fiber) : fiber.Priority) == priority);

    private int CurrentNativeRequestSlot(ActorState actor, Fiber? incoming = null) => fibers.Where(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete && fiber != incoming).Select(NativeActorRequestSlot).DefaultIfEmpty(0).Max();

    private string? UnsupportedNativeMovementRequest(ActorState actor, int priority, int scriptId)
    {
        //51CD60/51CED0/51D060 back up and stop only physical mode572=1.
        //Modes3/4 keep their current body/phase while the higher VM slot runs;
        //the held lower LADDER consumer neither suspends nor gains a backup.
        if (NativeRuntimeProfile is not { IsVerified: true } || priority <= CurrentNativeRequestSlot(actor))
        {
            return null;
        }
        if (actor.NativeMovementRequest is { } existing)
        {
            if (CanTransferNativeMovementBackup(actor, existing))
            {
                return UnsupportedNativeRequestResumption(actor, scriptId);
            }
            if (actor.NativeScriptMovement is not { Active: true, Suspended: false })
            {
                return "Native request requires its actual physical mode0/1 body before changing the retained backup owner";
            }
        }
        if (actor.NativeScriptMovement is not { Active: true } movement)
        {
            return null;
        }
        Fiber? owner = fibers.FirstOrDefault(fiber => fiber.Sequence == movement.FiberSequence && !fiber.Complete);
        if (owner is null && !movement.Asynchronous)
        {
            return "Native synchronous mode1 request requires its retained issued VM consumer";
        }
        if (owner is null && !fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete))
        {
            return "Native physical-only mode1 request requires a represented current VM slot; an empty slot table does not establish BYTE372";
        }
        if (owner is not null && priority <= NativeActorRequestSlot(owner))
        {
            return null;
        }
        if (movement.Suspended || actor.NativeJump is { Active: true })
        {
            return "Native request requires its single original movement backup without unproved nested overwrite";
        }
        return UnsupportedNativeRequestResumption(actor, scriptId);
    }

    private string? UnsupportedNativeRequestResumption(ActorState actor, int scriptId)
    {
        EventScript requested = program!.Objects[actor.ObjectId].Scripts.Single(script => script.Id == scriptId);
        if (program.Operations.Skip(requested.Start).Take(requested.End - requested.Start).Any(operation => operation.Opcode == Jsm.Opcode.RET && operation.Parameter != 8) || fibers.Where(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete).Sum(fiber => fiber.Stack.Count + 8) + 8 > 80)
        {
            return "Native movement request requires RET8 resumption and the original eighty-word operand/result stack";
        }
        return null;
    }

    private void SuspendNativeMovementForRequest(ActorState actor, Fiber requested)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || requested.Priority <= CurrentNativeRequestSlot(actor, requested))
        {
            return;
        }
        if (actor.NativeMovementRequest is { } existing && CanTransferNativeMovementBackup(actor, existing))
        {
            //Mode572=0 leaves448..456/540/514 untouched regardless of the
            //current slot's VM wait. Animation/WAIT/MES/HALT do not own
            //another backup; the next higher RET consumes this same one.
            actor.NativeMovementRequest = existing with { RequestSequence = requested.Sequence, RequestScriptId = requested.Script.Id, RequestSlot = (byte)requested.Priority };
            requested.NativeActorSlot = (byte)requested.Priority;
            return;
        }
        if (actor.NativeScriptMovement is not { Active: true } movement)
        {
            return;
        }
        Fiber? owner = fibers.FirstOrDefault(fiber => fiber.Sequence == movement.FiberSequence && !fiber.Complete);
        if (owner is null && !movement.Asynchronous)
        {
            throw new NotSupportedException("Native synchronous mode1 backup lost its issued VM consumer.");
        }
        if (owner is null && !fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && fiber != requested && !fiber.Complete))
        {
            throw new NotSupportedException("Native physical-only mode1 backup lost its represented selected VM slot.");
        }
        if (owner is not null && requested.Priority <= NativeActorRequestSlot(owner))
        {
            return;
        }
        // REQ51CD60/REQSW51CED0/REQEW51D060 save436..444/538/510
        // into the one physical448..456/540/514 backup, then set572=0.
        // Async mode1 can outlive its issuing VM slot; the physical backup
        // still owns the executed movement receipt without recreating a fiber.
        // A later mode1 preemption overwrites that backup. Other held fibers
        // retain only their VM issued-bit/operand receipt, never old scalars.
        actor.NativeMovementRequest = new(1, requested.Sequence, requested.Script.Id, (byte)requested.Priority, movement);
        actor.NativeScriptMovement = movement with { Suspended = true };
        requested.NativeActorSlot = (byte)requested.Priority;
    }

    private bool CanTransferNativeMovementBackup(ActorState actor, NativeMovementRequestState request) => actor.NativeScriptMovement is { Active: false } or { Suspended: true } && actor.NativeJump is not { Active: true } && fibers.Any(fiber => fiber.Sequence == request.RequestSequence && fiber.ObjectId == actor.ObjectId && NativeActorRequestSlot(fiber) == request.RequestSlot && !fiber.Complete);

    private static bool IsNativeMovementRequestIssue(ActorState actor, Fiber fiber) => actor.NativeMovementRequest is { } request && request.RequestSequence == fiber.Sequence && actor.NativeScriptMovement is { Suspended: true } movement && movement.FiberSequence == request.SavedMovement.FiberSequence;

    private void CompleteNativeMovementRequest(Fiber fiber, int returnSlot)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (actor.NativeMovementRequest is not { } request || request.RequestSequence != fiber.Sequence)
        {
            return;
        }
        if (returnSlot != 8)
        {
            throw new NotSupportedException("Native movement backup requires proved RET8 lower-slot resumption.");
        }
        FieldFixedPosition raw = ReadNativeScriptPosition(actor) ?? throw new NotSupportedException("Native RET8 movement restoration lost its actual raw body placement.");
        NativeScriptMovementState? current = actor.NativeScriptMovement;
        NativeJumpState? jump = actor.NativeJump;
        bool animationFlag = current is not null ? current.AnimationFlag40000 ?? (current.Suspended ? current.ResetAnimationOnCompletion ?? current.AutomaticAnimation : current.Active && (current.ResetAnimationOnCompletion ?? current.AutomaticAnimation)) : jump?.AnimationFlag40000 ?? throw new NotSupportedException("Native RET8 requires the jump's preserved physical movement-animation flag.");
        short turn = actor.NativeTurnAccumulator474 ?? throw new NotSupportedException("Native RET8 requires its current unbacked physical WORD474.");
        NativeMotionAuthority authority = actor.NativeMotionAuthority ?? throw new NotSupportedException("Native RET8 requires preserved current movement575 and facing hold576.");
        NativeScriptMovementState restored = request.SavedMovement with { FixedX = raw.X, FixedY = raw.Y, FixedZ = raw.Z, Triangle = actor.WalkmeshTriangle, FacingDirection = (byte)actor.Direction, MovementDirection = authority.MovementDirection, Suspended = false, AnimationFlag40000 = animationFlag, FacingHold576 = authority.FacingHold == (request.SavedMovement.HoldsFacing ? 1 : 0) ? null : authority.FacingHold };
        actor.NativeScriptMovement = restored;
        actor.NativeMovementCompletion542 = 0;
        actor.NativeTurnAccumulator474 = animationFlag ? (short)0 : turn;
        actor.NativeJump = null;
        actor.NativeLadder = null;
        actor.NativeLadderMode3 = null;
        actor.NativeMovementRequest = null;
        actor.MovementSpeed = restored.Speed;
        actor.NativeMotionAuthority = authority;
        actor.MovementTarget = new Vector3(restored.TargetX / 4096f, restored.TargetY / 4096f, restored.TargetZ / 4096f);
        actor.MovementStopDistance = restored.StopDistance;
        if (animationFlag)
        {
            int animation = restored.Speed < 2696 ? actor.WalkAnimationId : actor.RunAnimationId;
            if (actor.NativeAnimationHeader?.AnimationId != animation)
            {
                SelectFullNativeAnimation(actor, animation, true);
            }
        }
    }

    private void ValidateNativeMovementRequests(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        foreach (ActorState actor in saved.Actors.Where(actor => actor.NativeMovementRequest is not null))
        {
            NativeMovementRequestState request = actor.NativeMovementRequest!;
            NativeScriptMovementState movement = request.SavedMovement;
            bool partyTarget = IsNativePartyMove(movement.Operation);
            Fiber? lower = restored.SingleOrDefault(fiber => fiber.Sequence == movement.FiberSequence);
            Fiber? upper = restored.SingleOrDefault(fiber => fiber.Sequence == request.RequestSequence);
            EventScript? issuingScript = program!.Objects[actor.ObjectId].Scripts.FirstOrDefault(script => script.Id == movement.ScriptId);
            bool validLower = lower is null ? movement.Asynchronous : lower.ObjectId == actor.ObjectId && lower.Script.Id == movement.ScriptId && (movement.Asynchronous && lower.Complete || !lower.Complete && NativeActorRequestSlot(lower) < request.RequestSlot);
            if (movement.Asynchronous && (lower is null || lower.Complete) && !restored.Any(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete && NativeActorRequestSlot(fiber) < request.RequestSlot))
            {
                throw new InvalidDataException("Saved physical-only movement backup lost its represented lower VM slot.");
            }
            if (partyTarget && (movement.Asynchronous || !movement.AutomaticAnimation || movement.HoldsFacing || movement.Accelerated || lower is null || lower.Stack.Count == 0 || !lower.Stack.PeekValue().IsExact || (uint)lower.Stack.Peek() >= 3 || lower.NativeMoveTarget != (int)lower.Stack.Peek()))
            {
                throw new InvalidDataException("Saved PMOVEA backup lost its original full DWORD party slot.");
            }
            bool jumpBody = actor.NativeJump is { AnimationFlag40000: not null, AsyncFlag20000: not null } jump && jump.FiberSequence == request.RequestSequence && jump.ScriptId == request.RequestScriptId && MatchesNativeJumpPlacement(actor, jump) && actor.NativeMotionAuthority is { Version: 1, FacingHold: <= 1 };
            if (request.Version != 1 || request.RequestSlot > 7 || !validLower || issuingScript is null || upper is null || upper.Complete || upper.ObjectId != actor.ObjectId || upper.Script.Id != request.RequestScriptId || upper.NativeActorSlot != request.RequestSlot || movement.Version != 1 || !movement.Active || movement.Suspended || !movement.Asynchronous && (movement.Operation != lower!.Position || lower.WaitReason != WaitReason.NativeScriptMovement) || (uint)movement.Operation >= program.Operations.Count || program.Operations[movement.Operation].Opcode is not (Jsm.Opcode.DIRP or Jsm.Opcode.MOVE or Jsm.Opcode.MACCEL or Jsm.Opcode.FMOVE or Jsm.Opcode.MOVEA or Jsm.Opcode.PMOVEA or Jsm.Opcode.CMOVE or Jsm.Opcode.RMOVE or Jsm.Opcode.RFMOVE or Jsm.Opcode.RCMOVE or Jsm.Opcode.RMOVEA or Jsm.Opcode.RPMOVEA) || movement.TargetNativeObjectId is int target && !movement.Asynchronous && !partyTarget && (lower!.Stack.Count == 0 || lower.Stack.Peek() != target) || actor.NativeScriptMovement is null && !jumpBody)
            {
                throw new InvalidDataException("Saved native movement request lost its single backup, original lower operands or higher-slot owner.");
            }
            bool asyncPartyTarget = IsNativeAsyncPartyMove(movement.Operation);
            string opcode = asyncPartyTarget ? "RPMOVEA" : partyTarget ? "PMOVEA" : IsNativeDirectionPoint(movement) ? "DIRP" : movement.Accelerated ? "MACCEL" : movement.HoldsFacing ? movement.Asynchronous ? "RCMOVE" : "CMOVE" : movement.TargetNativeObjectId is not null ? movement.Asynchronous ? "RMOVEA" : "MOVEA" : movement.Asynchronous ? movement.AutomaticAnimation ? "RMOVE" : "RFMOVE" : movement.AutomaticAnimation ? "MOVE" : "FMOVE";
            if (asyncPartyTarget && (!movement.Asynchronous || !movement.AutomaticAnimation || movement.HoldsFacing || movement.Accelerated || movement.TargetNativeObjectId is null))
            {
                throw new InvalidDataException("Saved RPMOVEA backup lost its captured raw target or asynchronous mode1 policy.");
            }
            IEnumerable<int> resultRegisters = lower is null ? upper.Results.Keys : lower.Results.Keys.Concat(upper.Results.Keys);
            if (movement.Accelerated && (!movement.HoldsFacing || !movement.AutomaticAnimation || movement.Asynchronous || movement.TargetNativeObjectId is not null) || movement.FacingHold576 > 1 || movement.Cancelled || (IsNativeDirectionPoint(movement) ? movement.Speed != 0 || movement.StopDistance != 0 || movement.Asynchronous || movement.AutomaticAnimation || movement.HoldsFacing || movement.Accelerated || movement.TargetNativeObjectId is not null : movement.Speed <= 0) || movement.Triangle is < 0 or > short.MaxValue || (movement.FixedZ & 4095) != 0 || new[] { movement.FixedX, movement.FixedY, movement.FixedZ, movement.TargetX, movement.TargetY, movement.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || movement.TargetNativeObjectId is null && !partyTarget && new[] { movement.TargetX, movement.TargetY, movement.TargetZ }.Any(value => (value & 4095) != 0) || movement.TargetNativeObjectId is int targetId && ((uint)targetId >= program.NativeObjects.Count || !saved.Scene.NativeActorObjectIds.Contains(program.NativeObjects[targetId]) || !movement.AutomaticAnimation) || movement.HoldsFacing && !movement.Accelerated && (movement.AutomaticAnimation || movement.TargetNativeObjectId is not null) || movement.ResetAnimationOnCompletion is not null && (movement.ResetAnimationOnCompletion != true || movement.AutomaticAnimation || movement.TargetNativeObjectId is not null) || program.Operations[movement.Operation].Opcode.ToString() != opcode || movement.Operation < issuingScript.Start || movement.Operation >= issuingScript.End || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || upper.Priority != request.RequestSlot || resultRegisters.Any(register => register is < 0 or > 7) || restored.Where(fiber => fiber.ObjectId == actor.ObjectId && !fiber.Complete).Sum(fiber => fiber.Stack.Count + 8) > 80)
            {
                throw new InvalidDataException("Saved native movement backup has invalid raw domains, mode, animation policy or original instruction.");
            }
        }
        if (restored.Any(fiber => fiber.NativeMoveTarget is not null && fiber.NativeMoveOperation is null))
        {
            throw new InvalidDataException("Saved target operand receipt lacks its issued native instruction.");
        }
        foreach (Fiber fiber in restored.Where(fiber => fiber.NativeMoveOperation is not null))
        {
            int operation = fiber.NativeMoveOperation!.Value;
            bool partyTarget = IsNativePartyMove(operation);
            if (partyTarget && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || (uint)fiber.Stack.Peek() >= 3 || fiber.NativeMoveTarget != (int)fiber.Stack.Peek()))
            {
                throw new InvalidDataException("Saved issued PMOVEA lost its original full DWORD party-slot operand.");
            }
            if (fiber.Complete || fiber.WaitReason != WaitReason.NativeScriptMovement || operation != fiber.Position || operation < fiber.Script.Start || operation >= fiber.Script.End || (uint)operation >= program!.Operations.Count || program.Operations[operation].Opcode is not (Jsm.Opcode.DIRP or Jsm.Opcode.MOVE or Jsm.Opcode.MACCEL or Jsm.Opcode.FMOVE or Jsm.Opcode.CMOVE or Jsm.Opcode.MOVEA or Jsm.Opcode.PMOVEA) || program.Operations[operation].Opcode == Jsm.Opcode.MOVEA && (fiber.Stack.Count == 0 || fiber.NativeMoveTarget != (int)fiber.Stack.Peek() || (uint)(int)fiber.Stack.Peek() >= program.NativeObjects.Count || !saved.Scene.NativeActorObjectIds.Contains(program.NativeObjects[(int)fiber.Stack.Peek()])) || !partyTarget && program.Operations[operation].Opcode != Jsm.Opcode.MOVEA && fiber.NativeMoveTarget is not null)
            {
                throw new InvalidDataException("Saved issued native movement lost its held instruction or remaining target operand.");
            }
        }
        foreach (Fiber fiber in restored.Where(fiber => fiber.WaitReason == WaitReason.NativeHalt))
        {
            if (fiber.Complete || (uint)fiber.Position >= program!.Operations.Count || program.Operations[fiber.Position].Opcode != Jsm.Opcode.HALT)
            {
                throw new InvalidDataException("Saved native HALT lost its held original instruction and occupied slot.");
            }
        }
    }
}
