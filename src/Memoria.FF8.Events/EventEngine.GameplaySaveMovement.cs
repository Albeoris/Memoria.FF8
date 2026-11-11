using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<ActorState, FieldFixedPosition, bool>? CanRestoreNativeScriptMovementPlacement { get; set; }

    private bool IsGameplayMovementBackupPlacement(ActorState actor, SceneEventState scene, NativeScriptMovementState backup)
    {
        // RET8 replaces every backed-up position/triangle before resuming the lower worker.
        // Semantic reconstruction uses its current actor pose, which may be airborne.
        return scene.NativeFieldInitialization is { Origin: NativeFieldInitializationOrigin.GameplayContinuation } && actor.GameplayReconstruction is { Position: { } raw } origin && origin.Version == 1 && origin.FieldId == FieldId && origin.ProgramHash == programHash && origin.ObjectId == actor.ObjectId && raw.X == backup.FixedX && raw.Y == backup.FixedY && raw.Z == backup.FixedZ;
    }

    private int GameplayMovementObject(int nativeIndex)
    {
        if (program is null || (uint)nativeIndex >= program.NativeObjects.Count)
        {
            throw new InvalidDataException("Gameplay movement names an absent native object-table entry.");
        }
        return program.NativeObjects[nativeIndex];
    }

    private int GameplayMovementIndex(int objectId)
    {
        if (program is not null)
        {
            for (int index = 0; index < program.NativeObjects.Count; index++)
            {
                if (program.NativeObjects[index] == objectId)
                {
                    return index;
                }
            }
        }
        throw new InvalidDataException("Gameplay movement tracking target is absent from the current native program.");
    }

    private GameplaySaveMovementWorker CaptureGameplayMovementWorker(NativeScriptMovementState worker)
    {
        return new(worker.Active, worker.FiberSequence, worker.ScriptId, worker.Operation, new(worker.TargetX, worker.TargetY, worker.TargetZ), worker.Speed, worker.StopDistance, worker.MovementDirection, worker.FacingDirection, worker.Asynchronous, worker.AutomaticAnimation, worker.TargetNativeObjectId is int target ? GameplayMovementObject(target) : null, worker.ResetAnimationOnCompletion, worker.Cancelled, worker.HoldsFacing, worker.Suspended, worker.AnimationFlag40000, worker.FacingHold576, worker.Accelerated) { SupersededByPartyJoin = worker.SupersededByPartyJoin };
    }

    private GameplaySaveActorMovement? CaptureGameplayMovement(ActorState actor)
    {
        bool issued = fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && fiber.NativeMoveOperation is not null);
        bool needed = issued || actor.NativeMovementRequest is not null || actor.NativeScriptMovement is { Active: true } or { Cancelled: true };
        // An issued MOVE can outlive mode1 and poll the saved jump's completion.
        if (!needed || actor.NativeJump is not null && actor.NativeScriptMovement is null && actor.NativeMovementRequest is null)
        {
            return null;
        }
        GameplaySaveMovementWorker? current = actor.NativeScriptMovement is { } worker ? CaptureGameplayMovementWorker(worker) : null;
        GameplaySaveMovementBackup? backup = actor.NativeMovementRequest is { } request ? new(request.RequestSequence, request.RequestScriptId, request.RequestSlot, CaptureGameplayMovementWorker(request.SavedMovement), request.RestoreSuppressedByFlush) : null;
        GameplaySaveActorMovement result = new(current, backup);
        result.Validate();
        return result;
    }

    private NativeScriptMovementState RestoreGameplayMovementWorker(GameplaySaveMovementWorker saved, ActorState actor, GameplaySaveActorPosition raw)
    {
        saved.Validate();
        return new(1, saved.Active, saved.FiberSequence, saved.ScriptId, saved.Operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, saved.Target.X, saved.Target.Y, saved.Target.Z, saved.Speed, saved.StopDistance, saved.MovementDirection, saved.FacingDirection, saved.Asynchronous, saved.AutomaticAnimation, saved.TrackingObjectId is int target ? GameplayMovementIndex(target) : null, saved.ResetAnimation, saved.Cancelled, saved.HoldsFacing, saved.Suspended, saved.AnimationFlag, saved.FacingHold, saved.Accelerated, saved.SupersededByPartyJoin);
    }

    private void RestoreGameplayMovement(ActorState actor, GameplaySaveFieldActor saved)
    {
        if (saved.Movement is not { } movement)
        {
            return;
        }
        movement.Validate();
        GameplaySaveActorPosition raw = saved.Pose.FixedPosition ?? throw new InvalidDataException("Gameplay movement requires its canonical exact raw actor placement.");
        actor.NativeScriptMovement = movement.Worker is { } worker ? RestoreGameplayMovementWorker(worker, actor, raw) : null;
        if (movement.Backup is { } backup)
        {
            // RET8 overwrites the backed-up placement/direction with the actual current body.
            NativeScriptMovementState backed = RestoreGameplayMovementWorker(backup.Worker, actor, raw);
            actor.NativeMovementRequest = new(1, backup.RequestSequence, backup.RequestScriptId, backup.RequestSlot, backed, backup.RestoreSuppressed);
        }
        if (actor.NativeScriptMovement is { } current)
        {
            actor.MovementTarget = current.Cancelled || actor.NativeMovementCompletion542 == 2 || !current.Active ? null : new Vector3(current.TargetX / 4096f, current.TargetY / 4096f, current.TargetZ / 4096f);
            actor.MovementStopDistance = current.SupersededByPartyJoin ? 0 : current.StopDistance;
        }
    }

    private GameplaySaveMovementIssue? CaptureGameplayMovementIssue(Fiber fiber)
    {
        if (fiber.NativeMoveOperation is not int operation)
        {
            if (fiber.NativeMoveTarget is not null)
            {
                throw new InvalidDataException("Gameplay movement target operand has no issued VM instruction.");
            }
            return null;
        }
        bool party = IsNativePartyMove(operation);
        return new(operation, !party && fiber.NativeMoveTarget is int target ? GameplayMovementObject(target) : null, party ? fiber.NativeMoveTarget : null);
    }

    private void RestoreGameplayMovementIssue(Fiber fiber, GameplaySaveMovementIssue? saved)
    {
        if (saved is null)
        {
            return;
        }
        if (saved.Operation < 0 || saved.TrackingObjectId < 0 || saved.PartySlot is < 0 or > 2 || saved.TrackingObjectId is not null && saved.PartySlot is not null)
        {
            throw new InvalidDataException("Gameplay movement issue contains an invalid logical operand.");
        }
        fiber.NativeMoveOperation = saved.Operation;
        fiber.NativeMoveTarget = saved.PartySlot ?? (saved.TrackingObjectId is int target ? GameplayMovementIndex(target) : null);
    }

    private void ValidateGameplayMovement(IReadOnlyList<Fiber> current)
    {
        Dictionary<long, Fiber> bySequence = current.ToDictionary(fiber => fiber.Sequence);
        ValidateNativeScriptMovements(actors, Scene, current, bySequence);
        ValidateNativeMovementRequests(actors, Scene, current);
        ValidateNativeJoinedPositions(actors, Scene, FieldId, Frame, partyJoin);
        foreach (ActorState actor in actors.Where(actor => actor.NativeScriptMovement is { Active: true } or { Cancelled: true } || actor.NativeMovementRequest is not null || actor.NativeJump is null && current.Any(fiber => fiber.ObjectId == actor.ObjectId && fiber.NativeMoveOperation is not null)))
        {
            FieldFixedPosition? raw = ReadNativeScriptPosition(actor);
            if (raw is null || !extractingLegacyGameplay && (CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || CanRestoreNativeScriptMovementPlacement?.Invoke(actor, raw) != true))
            {
                throw new NotSupportedException("Gameplay movement requires current native movement providers and supported current-resource placement.");
            }
        }
    }
}
