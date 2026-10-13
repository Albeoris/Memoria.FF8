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
public sealed record NativeScriptMovementState(int Version, bool Active, long FiberSequence, int ScriptId, int Operation, int FixedX, int FixedY, int FixedZ, int Triangle, int TargetX, int TargetY, int TargetZ, short Speed, short StopDistance, byte Completion, byte MovementDirection, byte FacingDirection, short TurnAccumulator, byte DirectionLimit, bool Asynchronous = false, bool AutomaticAnimation = true, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TargetNativeObjectId = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ResetAnimationOnCompletion = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Cancelled = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool HoldsFacing = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Suspended = false, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? AnimationFlag40000 = null);

public sealed partial class EventEngine
{
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
        if (program!.Operations[fiber.Position].Opcode is Jsm.Opcode.RFMOVE or Jsm.Opcode.RMOVE && actor.NativeMotionAuthority is not { Version: 1, FacingHold: 0 })
        {
            return "Native RFMOVE requires known native motion scalars and cleared facing hold before its asynchronous issue";
        }
        if (actor.NativeMotionAuthority is { Version: not 1 } || !holdsFacing && actor.NativeMotionAuthority is { FacingHold: not 0 })
        {
            return "Native MOVE requires schema1 motion authority with facing hold576 cleared";
        }
        bool replacingRequest = IsNativeMovementRequestIssue(actor, fiber);
        if (!replacingRequest && actor.NativeScriptMovement is { Active: true, Asynchronous: false } running)
        {
            if (running.TargetNativeObjectId is int targetId && (fiber.Stack.Count == 0 || fiber.Stack.Peek() != targetId || ReadNativeActorMoveTarget(targetId) is null))
            {
                return "Native MOVEA requires its remaining original target operand and preserved raw target placement";
            }
            return running.FiberSequence == fiber.Sequence && running.Operation == fiber.Position && CanUseNativeScriptMove?.Invoke(actor) == true && ResolveNativeScriptDirection is not null && ResolveNativeScriptMovement is not null && actor.X == running.FixedX / 4096f && actor.Y == running.FixedY / 4096f && actor.Z == running.FixedZ / 4096f && actor.WalkmeshTriangle == running.Triangle && actor.Direction == running.FacingDirection ? null : "Persisted native MOVE requires its original owner, raw placement and certified movement adapters";
        }
        bool replacingAsync = replacingRequest || actor.NativeScriptMovement is { Active: true, Asynchronous: true };
        bool directCavernMove = FieldId == 129 && actor.ObjectId == 5 && actor.CharacterId == 3 && actor.MovementDirectionLimit == 0 && actor.NativeMotionAuthority is { Version: 1, FacingHold: 0 };
        bool actorTarget = program.Operations[fiber.Position].Opcode is Jsm.Opcode.MOVEA or Jsm.Opcode.RMOVEA;
        if (CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || !replacingAsync && actor.MovementTarget is not null || actor.MovementActorId is not null || actor.RotationTarget is not null && !actor.RotationUsesNativeCosine && !actor.RotationUsesNativeLinear || actor.PushRadius is < 0 or > ushort.MaxValue || actor.MovementSpeed is <= 0 or > short.MaxValue || !directCavernMove && actor.Direction != MathF.Truncate(actor.Direction) || !float.IsFinite(actor.Direction) || actor.Direction is < 0 or > 255 || fiber.Stack.Count < (actorTarget ? 2 : 4))
        {
            return $"Native MOVE requires its certified retained actor, original body direction, completed prior motion and signed speed (certified={CanUseNativeScriptMove?.Invoke(actor)}, retained={Scene.NativeActorObjectIds.Contains(actor.ObjectId)}, direction={actor.Direction}, speed={actor.MovementSpeed}, radius={actor.PushRadius}, target={actor.MovementTarget}, followTarget={actor.MovementActorId}, rotation={actor.RotationTarget}, operands={fiber.Stack.Count}, replacement={replacingAsync})";
        }
        if (!TryReadNativeBaseAnimations(actor, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(actor.ModelId, animation) is not > 0))
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
            NativeScriptMovementState first = PrepareNativeScriptStep(actor, CreateNativeScriptMove(fiber), issuing: true);
            if (actor.Direction != MathF.Truncate(actor.Direction) && first.Completion != 1)
            {
                return "Fractional prior facing requires an executed direct-direction MOVE step that overwrites native body577";
            }
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
            return new NativeScriptMovementState(1, true, fiber.Sequence, fiber.Script.Id, operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, target.X, target.Y, target.Z, (short)actor.MovementSpeed, (short)actorArgs[1], 0, movementDirection, (byte)actor.Direction, 0, actor.MovementDirectionLimit, Asynchronous: opcode == Jsm.Opcode.RMOVEA, TargetNativeObjectId: actorArgs[0]);
        }
        int[] args = fiber.Stack.Take(4).Reverse().ToArray();
        bool holdsFacing = opcode is Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE;
        bool automaticAnimation = opcode is not (Jsm.Opcode.RFMOVE or Jsm.Opcode.FMOVE or Jsm.Opcode.RCMOVE or Jsm.Opcode.CMOVE);
        bool? inheritedAnimation = !automaticAnimation && actor.NativeScriptMovement is { } previous && (previous.Active || previous.Cancelled) && (previous.ResetAnimationOnCompletion ?? previous.AutomaticAnimation) ? true : null;
        return new NativeScriptMovementState(1, true, fiber.Sequence, fiber.Script.Id, operation, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, args[0] << 12, args[1] << 12, args[2] << 12, (short)actor.MovementSpeed, (short)args[3], 0, movementDirection, (byte)actor.Direction, 0, holdsFacing ? (byte)0 : actor.MovementDirectionLimit, opcode is Jsm.Opcode.RFMOVE or Jsm.Opcode.RMOVE or Jsm.Opcode.RCMOVE, automaticAnimation, ResetAnimationOnCompletion: inheritedAnimation, HoldsFacing: holdsFacing);
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
        if (IsNativeMovementRequestIssue(actor, fiber) || actor.NativeScriptMovement is not { Active: true, Asynchronous: false })
        {
            actor.NativeScriptMovement = CreateNativeScriptMove(fiber, true);
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
                RegisterLegacyBaseAnimations(actor);
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
        if (actor.NativeScriptMovement.Asynchronous)
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
            actor.NativeScriptMovement = actor.NativeScriptMovement with { Active = false };
            actor.NativeMotionAuthority = new NativeMotionAuthority(1, actor.NativeScriptMovement.MovementDirection, 0);
            actor.MovementTarget = null;
            return operands;
        }
        if (actor.NativeScriptMovement.TargetNativeObjectId is int targetId)
        {
            FieldFixedPosition target = ReadNativeActorMoveTarget(targetId) ?? throw new NotSupportedException("Native MOVEA target lost its preserved raw placement.");
            actor.NativeScriptMovement = actor.NativeScriptMovement with { TargetX = target.X, TargetY = target.Y, TargetZ = target.Z };
            actor.MovementTarget = new Vector3(target.X / 4096f, target.Y / 4096f, target.Z / 4096f);
        }
        fiber.Position--;
        fiber.WaitReason = WaitReason.NativeScriptMovement;
        return operands;
    }

    private void CompleteNativeAsyncMovement(ActorState actor)
    {
        // Native529FF0 consumes flag20000/completion542 after this actor's VM.
        // RFMOVE preserves animation; RMOVE additionally carries flag40000.
        if (actor.NativeScriptMovement is { Active: true, Asynchronous: true, Completion: 2 } completed)
        {
            if (completed.AnimationFlag40000 ?? completed.ResetAnimationOnCompletion ?? completed.AutomaticAnimation)
            {
                SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
            }
            actor.NativeScriptMovement = completed with { Active = false };
            actor.NativeMotionAuthority = new NativeMotionAuthority(1, completed.MovementDirection, 0);
        }
    }

    private NativeScriptMovementState PrepareNativeScriptStep(ActorState actor, NativeScriptMovementState state, bool issuing = false)
    {
        byte held = state.HoldsFacing ? (byte)1 : (byte)0;
        bool resettingHold = issuing && state.HoldsFacing;
        if (CanUseNativeScriptMove?.Invoke(actor) != true || ResolveNativeScriptDirection is null || ResolveNativeScriptMovement is null || !resettingHold && state.DirectionLimit != actor.MovementDirectionLimit || state.Speed != actor.MovementSpeed || actor.NativeMotionAuthority is { Version: not 1 } || !resettingHold && actor.NativeMotionAuthority is { } authority && authority.FacingHold != held)
        {
            throw new NotSupportedException("Native scripted MOVE lost its certified actor, direction limit or speed; original substep preserved.");
        }
        NativeScriptDirectionQuery directionQuery = new(actor.ObjectId, state.FixedX, state.FixedY, state.TargetX, state.TargetY, state.Speed, state.StopDistance, (ushort)actor.PushRadius, state.MovementDirection, state.FacingDirection, state.TurnAccumulator, state.DirectionLimit, state.HoldsFacing);
        NativeScriptDirectionResult direction = ResolveNativeScriptDirection(directionQuery) ?? throw new NotSupportedException("Native scripted MOVE direction/stop proposal is outside its proved bounds.");
        if (!Enum.IsDefined(direction.Status) || direction.Status != NativeScriptDirectionStatus.Snapped && (direction.FixedX != state.FixedX || direction.FixedY != state.FixedY) || direction.Status == NativeScriptDirectionStatus.Snapped && (direction.FixedX != state.TargetX || direction.FixedY != state.TargetY) || direction.Status != NativeScriptDirectionStatus.Moving && (direction.Direction != state.MovementDirection || direction.TurnAccumulator != state.TurnAccumulator))
        {
            throw new NotSupportedException("Native scripted MOVE direction adapter returned inconsistent stop/snap state.");
        }
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
                if (query.HasActorCollision is not null)
                {
                    bool contact = EvaluateNativePlayerGroundContacts(actor, new NativePlayerGroundProbe(probe.FixedX, probe.FixedY, probe.NativeHeight)).Collision;
                    if (contact != probe.ActorCollision || movement.Status == NativeScriptMovementStatus.Accepted && ReferenceEquals(proposal, movement.Proposals[^1]) && contact)
                    {
                        throw new NotSupportedException("Native cavern MOVE actor-contact certificate differs from its retained peers or accepted final fan.");
                    }
                }
                else
                {
                    EnsureNativeScriptFanClear(actor, probe);
                }
            }
        }
        return state with { FixedX = movement.FixedX, FixedY = movement.FixedY, FixedZ = movement.FixedZ, Triangle = movement.Triangle, MovementDirection = movement.Direction, FacingDirection = state.HoldsFacing ? state.FacingDirection : movement.Direction, TurnAccumulator = direction.TurnAccumulator, Completion = 1 };
    }

    private void EnsureNativeScriptFanClear(ActorState moving, NativeScriptGroundProbe probe)
    {
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState peer = actors[objectId];
            if (peer.ObjectId == moving.ObjectId || peer.ModelId < 0 || peer.ModelId == ushort.MaxValue || !peer.CollisionEnabled)
            {
                continue;
            }
            if (peer.PushRadius is < 0 or > ushort.MaxValue)
            {
                throw new NotSupportedException("Native scripted MOVE peer radius exceeds its unsigned word.");
            }
            int radius = (moving.PushRadius + peer.PushRadius) / 2;
            FieldFixedPosition? raw = ReadNativeScriptPosition(peer);
            if (raw is not null)
            {
                int dz = (raw.Z >> 12) - probe.NativeHeight;
                if (dz <= -127 || dz >= 128)
                {
                    continue;
                }
                long dx = ((long)raw.X - probe.FixedX) >> 12;
                long dy = ((long)raw.Y - probe.FixedY) >> 12;
                long squared = dx * dx + dy * dy;
                if (squared > int.MaxValue)
                {
                    throw new NotSupportedException("Native scripted MOVE peer distance overflow requires its original wrapped comparison.");
                }
                if (squared >= (long)radius * radius)
                {
                    continue;
                }
            }
            else if (float.IsFinite(peer.X) && float.IsFinite(peer.Y) && peer.X is >= short.MinValue and <= short.MaxValue && peer.Y is >= short.MinValue and <= short.MaxValue)
            {
                // Historical managed motion has no native raw provenance. A
                // conservative +/-one-unit interval can certify distant peers
                // without inventing their fixed low bits or ground height.
                long dx = MinimumNativeScriptDifference(peer.X, probe.FixedX);
                long dy = MinimumNativeScriptDifference(peer.Y, probe.FixedY);
                if (dx * dx + dy * dy >= (long)radius * radius)
                {
                    continue;
                }
            }
            throw new NotSupportedException($"Native scripted MOVE fan contacts retained actor{peer.ObjectId}; original collision steering requires a wider certificate.");
        }
    }

    private static long MinimumNativeScriptDifference(float coordinate, int probe)
    {
        double fixedCoordinate = coordinate * 4096d;
        long low = (long)Math.Floor((fixedCoordinate - 4096 - probe) / 4096);
        long high = (long)Math.Floor((fixedCoordinate + 4096 - probe) / 4096);
        return low <= 0 && high >= 0 ? 0 : Math.Min(Math.Abs(low), Math.Abs(high));
    }

    private void StepNativeScriptMovement()
    {
        foreach (int objectId in Scene.NativeActorObjectIds)
        {
            ActorState actor = actors[objectId];
            if (partyJoin?.Targets.Any(target => target.ObjectId == objectId) == true)
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
            actor.NativeMotionAuthority = new NativeMotionAuthority(1, next.MovementDirection, next.HoldsFacing ? (byte)1 : (byte)0);
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
        if (IsCavernNativeMoveJoin)
        {
            StepJoinedCompanions(true);
        }
    }
}
