using Albeoris.Games.FF8.Jsm;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public enum NativeScriptDirectionStatus
{
    Moving,
    Stopped,
    Snapped
}

public enum NativeScriptMovementStatus
{
    Accepted,
    Blocked
}
public sealed record NativeScriptDirectionQuery(int ObjectId, int FixedX, int FixedY, int TargetX, int TargetY, short Speed, short StopDistance, ushort Radius, byte MovementDirection, byte FacingDirection, short TurnAccumulator, byte DirectionLimit, bool HoldsFacing = false);
public sealed record NativeScriptDirectionResult(NativeScriptDirectionStatus Status, int FixedX, int FixedY, byte Direction, short TurnAccumulator);
public sealed record NativeScriptMovementQuery(int ObjectId, int Triangle, int FixedX, int FixedY, int FixedZ, byte Direction, short Speed, ushort Radius, Func<NativeScriptGroundProbe, bool>? HasActorCollision = null);
public sealed record NativeScriptGroundProbe(int FixedX, int FixedY, int NativeHeight, bool ActorCollision = false);
public sealed record NativeScriptMovementProposal(int FixedX, int FixedY, byte Direction, IReadOnlyList<NativeScriptGroundProbe> FanProbes);
public sealed record NativeScriptMovementResult(NativeScriptMovementStatus Status, int FixedX, int FixedY, int FixedZ, int Triangle, byte Direction, IReadOnlyList<NativeScriptMovementProposal> Proposals);
/// <summary>Active movement owns the current body direction; an inactive completed record retains its last movement body direction and raw XYZ while later turns independently change actor577.</summary>
public sealed record NativeScriptMovementState(int Version, bool Active, long FiberSequence, int ScriptId, int Operation, int FixedX, int FixedY, int FixedZ, int Triangle, int TargetX, int TargetY, int TargetZ, short Speed, short StopDistance, byte Completion, byte MovementDirection, byte FacingDirection, short TurnAccumulator, byte DirectionLimit, bool Asynchronous = false, bool AutomaticAnimation = true, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TargetNativeObjectId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ResetAnimationOnCompletion = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Cancelled = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool HoldsFacing = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Suspended = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? AnimationFlag40000 = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? FacingHold576 = null);

public sealed partial class EventEngine
{
    private static byte NativeMoveFacingHold(NativeScriptMovementState movement) => movement.FacingHold576 ?? (movement.HoldsFacing ? (byte)1 : (byte)0);

    public Func<ActorState, bool>? CanUseNativeScriptMove { get; set; }
    public Func<NativeScriptDirectionQuery, NativeScriptDirectionResult?>? ResolveNativeScriptDirection { get; set; }
    public Func<NativeScriptMovementQuery, NativeScriptMovementResult?>? ResolveNativeScriptMovement { get; set; }

    private string? UnsupportedNativeScriptMove(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (program!.Operations[fiber.Position].Opcode == Jsm.Opcode.RMOVEA && NativeRuntimeProfile is not { IsVerified: true })
        {
            return "Native RMOVEA requires authenticated original executable provenance";
        }
        bool holdsFacing = program!.Operations[fiber.Position].Opcode is Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE;
        if (holdsFacing && actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 })
        {
            return "Native held-facing MOVE requires known native movement/facing-hold bytes before issue";
        }
        if (program!.Operations[fiber.Position].Opcode is Jsm.Opcode.RFMOVE or Jsm.Opcode.RMOVE && actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 })
        {
            return "Native RFMOVE requires known native motion scalars and preserved facing hold before its asynchronous issue";
        }
        if (actor.NativeMotionAuthority is { Version: not 1 } or { FacingHold: > 1 })
        {
            return "Native MOVE requires schema1 motion authority and known facing hold576";
        }
        bool replacingRequest = IsNativeMovementRequestIssue(actor, fiber);
        if (fiber.NativeMoveOperation is int issued)
        {
            bool actorTargetIssued = program.Operations[issued].Opcode == Jsm.Opcode.MOVEA;
            NativeScriptMovementState? physical = actor.NativeScriptMovement;
            if (issued != fiber.Position || physical is null || physical.Suspended || physical.Asynchronous || !physical.Active && !physical.Cancelled && physical.Completion != 2 || CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || ReadNativeScriptPosition(actor) is null || actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 } || actorTargetIssued && (fiber.Stack.Count == 0 || fiber.NativeMoveTarget != (int)fiber.Stack.Peek() || ReadNativeActorMoveTarget(fiber.Stack.Peek()) is null))
            {
                return "Issued native MOVE requires its held instruction, actual shared mode1 body and remaining target operand";
            }
            return null;
        }
        if (!replacingRequest && actor.NativeScriptMovement is { Active: true, Asynchronous: false } running)
        {
            if (running.TargetNativeObjectId is int targetId && (fiber.Stack.Count == 0 || fiber.Stack.Peek() != targetId || ReadNativeActorMoveTarget(targetId) is null))
            {
                return "Native MOVEA requires its remaining original target operand and preserved raw target placement";
            }
            return running.FiberSequence == fiber.Sequence && running.Operation == fiber.Position && CanUseNativeScriptMove?.Invoke(actor) == true && ResolveNativeScriptDirection is not null && ResolveNativeScriptMovement is not null && actor.X == running.FixedX / 4096f && actor.Y == running.FixedY / 4096f && actor.Z == running.FixedZ / 4096f && actor.WalkmeshTriangle == running.Triangle && actor.Direction == running.FacingDirection ? null : "Persisted native MOVE requires its original owner, raw placement and certified movement adapters";
        }
        bool replacingAsync = replacingRequest || actor.NativeScriptMovement is { Active: true, Asynchronous: true };
        bool actorTarget = program.Operations[fiber.Position].Opcode is Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA;
        if (CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || !replacingAsync && actor.MovementTarget is not null || actor.MovementActorId is not null || actor.RotationTarget is not null && !actor.RotationUsesNativeCosine && !actor.RotationUsesNativeLinear || actor.PushRadius is < 0 or > ushort.MaxValue || actor.MovementSpeed is <= 0 or > short.MaxValue || actor.Direction != MathF.Truncate(actor.Direction) || !float.IsFinite(actor.Direction) || actor.Direction is < 0 or > 255 || fiber.Stack.Count < (actorTarget ? 2 : 4))
        {
            return $"Native MOVE requires its certified retained actor, original body direction, completed prior motion and signed speed (certified={CanUseNativeScriptMove?.Invoke(actor)}, retained={Scene.NativeActorObjectIds.Contains(actor.ObjectId)}, direction={actor.Direction}, speed={actor.MovementSpeed}, radius={actor.PushRadius}, model={actor.ModelId}, authority={actor.NativeMotionAuthority}, initialization={Scene.NativeFieldInitialization}, target={actor.MovementTarget}, followTarget={actor.MovementActorId}, rotation={actor.RotationTarget}, operands={fiber.Stack.Count}, replacement={replacingAsync})";
        }
        if (!HasNativeEmptyAnimationModel(actor) && (!TryReadNativeBaseAnimations(actor, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(actor.ModelId, animation) is not > 0)))
        {
            return "Native MOVE requires original full idle/walk/run animation metadata";
        }
        int[] operands = fiber.Stack.Take(actorTarget ? 2 : 4).ToArray();
        if ((!actorTarget && operands.Skip(1).Any(value => value is < short.MinValue or > short.MaxValue)) || actorTarget && (operands[0] is < short.MinValue or > short.MaxValue || ReadNativeActorMoveTarget(operands[1]) is null) || ReadNativeScriptPosition(actor) is null)
        {
            return "Native MOVE requires preserved raw actor placement and bounded signed target coordinates";
        }
        try
        {
            PrepareNativeScriptStep(actor, CreateNativeScriptMove(fiber), issuing: true);
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private FieldFixedPosition? ReadNativeScriptPosition(ActorState actor)
    {
        if (actor.NativeJump is NativeJumpState jump && MatchesNativeJumpPlacement(actor, jump))
        {
            return new FieldFixedPosition(jump.FixedX, jump.FixedY, jump.FixedZ);
        }
        if (Scene.NativePartyTraceVersion == 1 && IsNativePartyPlayerBody(actor) && Scene.NativePlayerPosition is FieldFixedPosition player && actor.X == player.X / 4096f && actor.Y == player.Y / 4096f && actor.Z == player.Z / 4096f)
        {
            return player;
        }
        if (actor.NativeScriptMovement is NativeScriptMovementState raw && actor.X == raw.FixedX / 4096f && actor.Y == raw.FixedY / 4096f && actor.Z == raw.FixedZ / 4096f)
        {
            return new FieldFixedPosition(raw.FixedX, raw.FixedY, raw.FixedZ);
        }
        if (partyJoin?.Targets.FirstOrDefault(target => target.ObjectId == actor.ObjectId) is { } join && actor.X == join.FixedX / 4096f && actor.Y == join.FixedY / 4096f && actor.Z == join.FixedZ / 4096f)
        {
            return new FieldFixedPosition(join.FixedX, join.FixedY, join.FixedZ);
        }
        if (actor.NativeJoinedPosition is { } joined && actor.X == joined.FixedX / 4096f && actor.Y == joined.FixedY / 4096f && actor.Z == joined.FixedZ / 4096f && actor.WalkmeshTriangle == joined.Triangle)
        {
            return new FieldFixedPosition(joined.FixedX, joined.FixedY, joined.FixedZ);
        }
        if (partySplit?.Targets.FirstOrDefault(target => target.ObjectId == actor.ObjectId) is { } split && actor.X == split.FixedX / 4096f && actor.Y == split.FixedY / 4096f && actor.Z == split.FixedZ / 4096f)
        {
            return new FieldFixedPosition(split.FixedX, split.FixedY, split.FixedZ);
        }
        if (actor.NativeSplitPosition is { } completedSplit && actor.X == completedSplit.FixedX / 4096f && actor.Y == completedSplit.FixedY / 4096f && actor.Z == completedSplit.FixedZ / 4096f && actor.WalkmeshTriangle == completedSplit.Triangle)
        {
            return new FieldFixedPosition(completedSplit.FixedX, completedSplit.FixedY, completedSplit.FixedZ);
        }
        if (!float.IsFinite(actor.X) || !float.IsFinite(actor.Y) || !float.IsFinite(actor.Z) || actor.X != MathF.Truncate(actor.X) || actor.Y != MathF.Truncate(actor.Y) || actor.Z != MathF.Truncate(actor.Z) || actor.X is < short.MinValue or > short.MaxValue || actor.Y is < short.MinValue or > short.MaxValue || actor.Z is < short.MinValue or > short.MaxValue)
        {
            return null;
        }
        return new FieldFixedPosition((int)actor.X << 12, (int)actor.Y << 12, (int)actor.Z << 12);
    }

    private NativeScriptMovementState CreateNativeScriptMove(Fiber fiber, bool executing = false)
    {
        ActorState actor = actors[fiber.ObjectId];
        FieldFixedPosition raw = ReadNativeScriptPosition(actor)!;
        byte movementDirection = actor.NativeMotionAuthority?.MovementDirection ?? actor.NativeScriptMovement?.MovementDirection ?? (byte)actor.Direction;
        int operation = fiber.Position - (executing ? 1 : 0);
        Jsm.Opcode opcode = program!.Operations[operation].Opcode;
        if (opcode is Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA)
        {
            int[] actorArgs = fiber.Stack.Take(2).Reverse().ToArray();
            FieldFixedPosition target = ReadNativeActorMoveTarget(actorArgs[0]) ?? throw new NotSupportedException("Native MOVEA target has no preserved raw placement.");
            return new NativeScriptMovementState(1, true, fiber.Sequence, fiber.Script.Id, operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, target.X, target.Y, target.Z, (short)actor.MovementSpeed, (short)actorArgs[1], 0, movementDirection, (byte)actor.Direction, 0, actor.MovementDirectionLimit, Asynchronous: opcode == Jsm.Opcode.RMOVEA, TargetNativeObjectId: actorArgs[0], FacingHold576: actor.NativeMotionAuthority?.FacingHold == 1 ? (byte?)1 : null);
        }
        int[] args = fiber.Stack.Take(4).Reverse().ToArray();
        bool holdsFacing = opcode is Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE;
        bool automaticAnimation = opcode is not (Jsm.Opcode.RFMOVE or Jsm.Opcode.FMOVE or Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE);
        bool? inheritedAnimation = !automaticAnimation && actor.NativeScriptMovement is { } previous && (previous.Active || previous.Cancelled) && (previous.AnimationFlag40000 ?? previous.ResetAnimationOnCompletion ?? previous.AutomaticAnimation) ? true : null;
        return new NativeScriptMovementState(1, true, fiber.Sequence, fiber.Script.Id, operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, args[0] << 12, args[1] << 12, args[2] << 12, (short)actor.MovementSpeed, (short)args[3], 0, movementDirection, (byte)actor.Direction, 0, holdsFacing ? (byte)0 : actor.MovementDirectionLimit, opcode is Jsm.Opcode.RFMOVE or Jsm.Opcode.RMOVE or Jsm.Opcode.RCMOVE, automaticAnimation, ResetAnimationOnCompletion: inheritedAnimation, HoldsFacing: holdsFacing, FacingHold576: !holdsFacing && actor.NativeMotionAuthority?.FacingHold == 1 ? (byte?)1 : null);
    }

    private FieldFixedPosition? ReadNativeActorMoveTarget(int nativeObjectId)
    {
        if (program is null || (uint)nativeObjectId >= program.NativeObjects.Count)
        {
            return null;
        }
        ActorState target = actors[program.NativeObjects[nativeObjectId]];
        return Scene.NativeActorObjectIds.Contains(target.ObjectId) ? ReadNativePlayerContactPosition(target) : null;
    }

    private int[] ExecuteNativeScriptMove(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        int[] operands = [];
        if (fiber.NativeMoveOperation is int issued && actor.NativeScriptMovement is { } shared && (shared.FiberSequence != fiber.Sequence || shared.Operation != issued))
        {
            // The native373 issued bit belongs to the VM slot, independently of
            // the shared572/542 body and its single physical backup. Return4
            // preserves that bit even after another request consumed the backup.
            Jsm.Opcode opcode = program!.Operations[issued].Opcode;
            bool held = opcode == Jsm.Opcode.CMOVE;
            bool automatic = opcode is Jsm.Opcode.MOVE or Jsm.Opcode.MOVEA;
            int? target = opcode == Jsm.Opcode.MOVEA ? (int)fiber.Stack.Peek() : null;
            actor.NativeScriptMovement = shared with { FiberSequence = fiber.Sequence, ScriptId = fiber.Script.Id, Operation = issued, Asynchronous = false, AutomaticAnimation = automatic, TargetNativeObjectId = target, HoldsFacing = held, ResetAnimationOnCompletion = null, AnimationFlag40000 = shared.AnimationFlag40000 ?? (shared.Active && (shared.ResetAnimationOnCompletion ?? shared.AutomaticAnimation)), FacingHold576 = actor.NativeMotionAuthority!.FacingHold == (held ? 1 : 0) ? null : actor.NativeMotionAuthority.FacingHold };
        }
        if (fiber.NativeMoveOperation is null)
        {
            actor.NativeScriptMovement = CreateNativeScriptMove(fiber, true);
            actor.NativeJoinedPosition = null;
            actor.NativeSplitPosition = null;
            actor.NativeJump = null;
            if (actor.NativeScriptMovement.HoldsFacing)
            {
                // 524490/523AD0 set576=1 and clear610 before mode1's first post-VM step.
                actor.NativeMotionAuthority = new NativeMotionAuthority(1, actor.NativeScriptMovement.MovementDirection, 1);
                actor.MovementDirectionLimit = 0;
            }
            operands = Pop(fiber, actor.NativeScriptMovement.TargetNativeObjectId is not null ? actor.NativeScriptMovement.Asynchronous ? 2 : 1 : 4);
            if (actor.NativeScriptMovement.AutomaticAnimation)
            {
                int animation = actor.MovementSpeed < 2696 ? actor.WalkAnimationId : actor.RunAnimationId;
                if (actor.AnimationId != animation)
                {
                    SelectFullNativeAnimation(actor, animation, true);
                }
            }
            actor.MovementTarget = new Vector3(actor.NativeScriptMovement.TargetX / 4096f, actor.NativeScriptMovement.TargetY / 4096f, actor.NativeScriptMovement.TargetZ / 4096f);
            actor.MovementStopDistance = actor.NativeScriptMovement.StopDistance;
            actor.MovementFlags = actor.NativeScriptMovement.StopDistance;
            actor.MovementIgnoresWalkmesh = false;
        }
        if (actor.NativeScriptMovement!.Asynchronous)
        {
            // Native524550 return3 advances PC and yields without waiting for542.
            WaitFrames(fiber, 1);
            return operands;
        }
        if (actor.NativeScriptMovement.Completion == 2)
        {
            if (actor.NativeScriptMovement.AnimationFlag40000 ?? actor.NativeScriptMovement.ResetAnimationOnCompletion ?? actor.NativeScriptMovement.AutomaticAnimation)
            {
                SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
            }
            if (actor.NativeScriptMovement.TargetNativeObjectId is not null)
            {
                Pop(fiber, 1);
            }
            actor.NativeScriptMovement = actor.NativeScriptMovement with { Active = false, Cancelled = false, AnimationFlag40000 = false };
            fiber.NativeMoveOperation = null;
            fiber.NativeMoveTarget = null;
            actor.NativeMotionAuthority = new NativeMotionAuthority(1, actor.NativeScriptMovement.MovementDirection, 0);
            actor.MovementTarget = null;
            return operands;
        }
        if (actor.NativeScriptMovement.TargetNativeObjectId is int targetId)
        {
            FieldFixedPosition target = ReadNativeActorMoveTarget(targetId) ?? throw new NotSupportedException("Native MOVEA target lost its preserved raw placement.");
            actor.NativeScriptMovement = actor.NativeScriptMovement with { TargetX = target.X, TargetY = target.Y, TargetZ = target.Z };
            actor.MovementTarget = actor.NativeScriptMovement.Cancelled ? null : new Vector3(target.X / 4096f, target.Y / 4096f, target.Z / 4096f);
        }
        fiber.Position--;
        fiber.NativeMoveOperation = fiber.Position;
        fiber.NativeMoveTarget = actor.NativeScriptMovement.TargetNativeObjectId;
        fiber.WaitReason = WaitReason.NativeScriptMovement;
        return operands;
    }

    private void CompleteNativeAsyncMovement(ActorState actor)
    {
        CompleteNativeJumpAsyncMovement(actor);
        // Native529FF0 consumes flag20000/completion542 after this actor's VM.
        // RFMOVE preserves animation; RMOVE additionally carries flag40000.
        if (actor.NativeScriptMovement is { Asynchronous: true, Completion: 2 } completed && (completed.Active || completed.Cancelled))
        {
            if (completed.AnimationFlag40000 ?? completed.ResetAnimationOnCompletion ?? completed.AutomaticAnimation)
            {
                SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
            }
            actor.NativeScriptMovement = completed with { Active = false, Cancelled = false, AnimationFlag40000 = false };
            actor.NativeMotionAuthority = new NativeMotionAuthority(1, completed.MovementDirection, 0);
        }
    }

    private NativeScriptMovementState PrepareNativeScriptStep(ActorState actor, NativeScriptMovementState state, bool issuing = false)
    {
        byte held = NativeMoveFacingHold(state);
        bool resettingHold = issuing && state.HoldsFacing;
        if (CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || !resettingHold && state.DirectionLimit != actor.MovementDirectionLimit || state.Speed != actor.MovementSpeed || actor.NativeMotionAuthority is { Version: not 1 } || !resettingHold && actor.NativeMotionAuthority is { } authority && authority.FacingHold != held)
        {
            throw new NotSupportedException("Native scripted MOVE lost its certified actor, direction limit or speed; original substep preserved.");
        }
        NativeScriptDirectionQuery directionQuery = new(actor.ObjectId, state.FixedX, state.FixedY, state.TargetX, state.TargetY, state.Speed, state.StopDistance, (ushort)actor.PushRadius, state.MovementDirection, state.FacingDirection, state.TurnAccumulator, state.DirectionLimit, held != 0);
        NativeScriptDirectionResult direction = PrepareNativeDirectionStep(directionQuery);
        if (direction.Status != NativeScriptDirectionStatus.Moving)
        {
            return state with { FixedX = direction.FixedX, FixedY = direction.FixedY, Completion = 2 };
        }
        NativeScriptMovementQuery query = new(actor.ObjectId, state.Triangle, state.FixedX, state.FixedY, state.FixedZ, direction.Direction, state.Speed, (ushort)actor.PushRadius, probe => EvaluateNativePlayerGroundContacts(actor, new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight)).Collision);
        NativeScriptMovementResult movement = ResolveNativeScriptMovement(query) ?? throw new NotSupportedException("Native scripted MOVE geometry needs unproved contact/retry behavior; original substep preserved.");
        if (!Enum.IsDefined(movement.Status) || movement.Proposals is null || movement.Proposals.Count is < 1 or > 16 || movement.Triangle is < 0 or > short.MaxValue || new[] { movement.FixedX, movement.FixedY, movement.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || movement.Proposals[0].Direction != direction.Direction || movement.Status == NativeScriptMovementStatus.Blocked && (movement.FixedX != state.FixedX || movement.FixedY != state.FixedY || movement.FixedZ != state.FixedZ) || movement.Status == NativeScriptMovementStatus.Accepted && (movement.FixedX != movement.Proposals[^1].FixedX || movement.FixedY != movement.Proposals[^1].FixedY || movement.Direction != movement.Proposals[^1].Direction || (movement.FixedZ & 4095) != 0))
        {
            throw new NotSupportedException("Native scripted MOVE geometry returned inconsistent accepted/blocked raw state.");
        }
        foreach (NativeScriptMovementProposal proposal in movement.Proposals)
        {
            if (proposal.FanProbes is null || proposal.FanProbes.Count != 3 || proposal.FanProbes.Any(probe => probe.NativeHeight is < short.MinValue or > short.MaxValue))
            {
                throw new NotSupportedException("Native scripted MOVE requires all three native ground-height fan contact queries.");
            }
            foreach (NativeScriptGroundProbe probe in proposal.FanProbes)
            {
                bool contact = EvaluateNativePlayerGroundContacts(actor, new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight)).Collision;
                if (contact != probe.ActorCollision || movement.Status == NativeScriptMovementStatus.Accepted && ReferenceEquals(proposal, movement.Proposals[^1]) && contact)
                {
                    throw new NotSupportedException("Native MOVE actor-contact certificate differs from its retained peers or accepted final fan.");
                }
            }
        }
        return state with { FixedX = movement.FixedX, FixedY = movement.FixedY, FixedZ = movement.FixedZ, Triangle = movement.Triangle, MovementDirection = movement.Direction, FacingDirection = held != 0 ? state.FacingDirection : movement.Direction, TurnAccumulator = direction.TurnAccumulator, Completion = 1 };
    }

    private NativeScriptDirectionResult PrepareNativeDirectionStep(NativeScriptDirectionQuery query)
    {
        NativeScriptDirectionResult direction = ResolveNativeScriptDirection?.Invoke(query) ?? throw new NotSupportedException("Native mode1 direction/stop proposal is outside its proved bounds.");
        if (!Enum.IsDefined(direction.Status) || direction.Status != NativeScriptDirectionStatus.Snapped && (direction.FixedX != query.FixedX || direction.FixedY != query.FixedY) || direction.Status == NativeScriptDirectionStatus.Snapped && (direction.FixedX != query.TargetX || direction.FixedY != query.TargetY) || direction.Status != NativeScriptDirectionStatus.Moving && (direction.Direction != query.MovementDirection || direction.TurnAccumulator != query.TurnAccumulator))
        {
            throw new NotSupportedException("Native mode1 direction adapter returned inconsistent stop/snap state.");
        }
        return direction;
    }

    private void StepNativeScriptMovement()
    {
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState actor = actors[objectId];
            // 524F40 issues mode572=1 for JOIN too.4789A0 commits each body
            // at its native index before later mode1 bodies inspect peer contacts.
            if (StepNativeJoinedCompanion(actor) || StepNativeSplitCompanion(actor))
            {
                continue;
            }
            if (actor.NativeScriptMovement is not { Active: true, Completion: < 2, Suspended: false } current)
            {
                continue;
            }
            NativeScriptMovementState next;
            try
            {
                // Native4789A0 samples580 rotation into577 before mode572 motion.
                // MOVE consumes current577; its normal moving result overwrites it.
                next = PrepareNativeScriptStep(actor, current with { FacingDirection = (byte)actor.Direction });
            }
            catch (NotSupportedException exception)
            {
                LastFault = new EventFault(FieldId, objectId, current.ScriptId, current.Operation, program!.Operations[current.Operation].Opcode.ToString(), exception.Message);
                throw new EventScriptException($"Field{FieldId} native MOVE{current.Operation}: {exception.Message}", exception);
            }
            actor.NativeScriptMovement = next;
            actor.NativeMotionAuthority = new NativeMotionAuthority(1, next.MovementDirection, NativeMoveFacingHold(next));
            actor.X = next.FixedX / 4096f;
            actor.Y = next.FixedY / 4096f;
            actor.Z = next.FixedZ / 4096f;
            actor.WalkmeshTriangle = next.Triangle;
            actor.Direction = next.FacingDirection;
            if (Scene.NativePartyTraceVersion == 1 && IsNativePartyPlayerBody(actor))
            {
                Scene.NativePlayerPosition = new FieldFixedPosition(next.FixedX, next.FixedY, next.FixedZ);
                ClearNativeDoorCurrentContacts();
            }
            if (next.Completion == 2)
            {
                actor.MovementTarget = null;
            }
        }
    }
}
