namespace Memoria.FF8.Events;

public sealed record PartySplitState(long FiberSequence, int CallerObjectId, PartyJoinTarget[] Targets);

public sealed partial class EventEngine
{
    private PartySplitState? partySplit;

    private FieldFixedPosition? ReadNativeSplitPosition(ActorState actor)
    {
        return ReadNativeScriptPosition(actor);
    }

    private bool CanUseNativeSplitMovement()
    {
        ActorState[] targets = Enumerable.Range(0, 3).Select(ResolveJoinPartyActor).OfType<ActorState>().ToArray();
        bool fresh = Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, FieldId, programHash) == true && Scene.NativeFieldInitialization.Stage == Memoria.FF8.Runtime.NativeFieldInitializationStage.Completed;
        return targets.Length != 0 && fresh && ResolveNativeJoinMovement is not null && ResolveNativeScriptDirection is not null && targets.All(actor => CanUseNativeJoinMovement?.Invoke(actor) == true);
    }

    private string? UnsupportedSplitContext(Fiber fiber)
    {
        if (partySplit is not null)
        {
            return LastFault is not null ? "Saved unsupported native SPLIT substep requires its original pre-SPLIT source" : partySplit.FiberSequence == fiber.Sequence ? null : "Concurrent SPLIT would replace original global party movement targets";
        }
        bool usesNativeMovement = CanUseNativeSplitMovement();
        if (partyJoin is not null || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || !usesNativeMovement)
        {
            return "Native SPLIT requires the original compact saved party bodies and shared native movement services";
        }
        int[] args = fiber.Stack.ToArray();
        if (args.Length < 9 || args.Take(9).Any(value => value is < short.MinValue or > short.MaxValue))
        {
            return "Native SPLIT requires nine original signed field-coordinate operands";
        }
        for (int slot = 0; slot < 3; slot++)
        {
            if (ResolveJoinPartyActor(slot) is not ActorState actor)
            {
                continue;
            }
            if (actor.NativeLadderMode3 is { Active: true })
            {
                return "Native SPLIT cannot replace an active mode3 party body without its physical overlap contract";
            }
            if (actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 } || ReadNativeSplitPosition(actor) is null || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.PushRadius is < 0 or > ushort.MaxValue)
            {
                return "Native SPLIT requires proved fixed actor positions, completed prior motion and native motion authority";
            }
            if (actor.NativeTurnAccumulator474 is null)
            {
                return "Native SPLIT requires its actual preserved WORD474 before target issue";
            }
            if (actor.ObjectId != fiber.ObjectId && fibers.Any(other => other.ObjectId == actor.ObjectId && !other.Complete && other.WaitReason != WaitReason.NativeHalt))
            {
                return "Native SPLIT competing companion scripts need original suspension support";
            }
            if (!TryReadNativeBaseAnimations(actor, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(actor.ModelId, animation) is not > 0))
            {
                return "Native SPLIT requires original full idle/walk/run animation metadata";
            }
        }
        return null;
    }

    private void ExecuteSplit(Fiber fiber)
    {
        if (partySplit is null)
        {
            int[] args = Pop(fiber, 9);
            List<PartyJoinTarget> targets = [];
            for (int slot = 0; slot < 3; slot++)
            {
                if (ResolveJoinPartyActor(slot) is not ActorState actor)
                {
                    continue;
                }
                FieldFixedPosition raw = ReadNativeSplitPosition(actor)!;
                int targetX = args[slot * 3] << 12;
                int targetY = args[slot * 3 + 1] << 12;
                int targetZ = args[slot * 3 + 2] << 12;
                int dx = unchecked(targetX - raw.X) / 4096;
                int dy = unchecked(targetY - raw.Y) / 4096;
                bool walk = unchecked(dx * dx + dy * dy) < 259200;
                int speed = (short)((walk ? 69020 : 203000) * 20 >> 9);
                actor.MovementSpeed = speed;
                actor.MovementStopDistance = 8;
                actor.NativeScriptMovement = null;
                // Native issue replaces mode572=2 with mode1; its former ballistic phase no longer owns this body.
                actor.NativeJump = null;
                actor.NativeLadder = null;
                actor.NativeLadderMode3 = null;
                actor.NativeMovementCompletion542 = 0;
                SelectFullNativeAnimation(actor, walk ? actor.WalkAnimationId : actor.RunAnimationId, true);
                targets.Add(new PartyJoinTarget(actor.ObjectId, raw.X, raw.Y, raw.Z, targetX, targetY, targetZ, speed));
            }
            partySplit = new PartySplitState(fiber.Sequence, fiber.ObjectId, targets.ToArray());
        }
        foreach (PartyJoinTarget target in partySplit.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            if (actors[target.ObjectId].NativeMovementCompletion542 == 2 && actor.NativeAnimationHeader?.AnimationId != actor.BaseAnimationId)
            {
                SelectNativeBodyAnimation(actor, unchecked((byte)actor.BaseAnimationId), AnimationHold);
            }
        }
        if (partySplit.Targets.Any(target => actors[target.ObjectId].NativeMovementCompletion542 != 2))
        {
            fiber.Position--;
            fiber.WaitReason = WaitReason.PartySplit;
            return;
        }
        // Native525090 clears movement/script blocks, but neither resets
        // stop538 nor rebuilds the existing party trace.
        foreach (PartyJoinTarget target in partySplit.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            actor.NativeSplitPosition = new NativeJoinedPosition(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, Frame, target.FixedX, target.FixedY, target.FixedZ, actor.WalkmeshTriangle, actor.NativeMotionAuthority!);
        }
        partySplit = null;
    }

    private bool StepNativeSplitCompanion(ActorState actor)
    {
        int index = partySplit is null ? -1 : Array.FindIndex(partySplit.Targets, target => target.ObjectId == actor.ObjectId);
        if (index < 0)
        {
            return false;
        }
        PartyJoinTarget target = partySplit!.Targets[index];
        if (actors[target.ObjectId].NativeMovementCompletion542 == 2)
        {
            return true;
        }
        if (ResolveNativeJoinMovement is null || ResolveNativeScriptDirection is null || !CanUseNativeSplitMovement() || actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 } authority || actor.NativeTurnAccumulator474 is null || (uint)actor.Direction > byte.MaxValue || actor.PushRadius is < 0 or > ushort.MaxValue || actor.MovementSpeed != target.Speed || actor.MovementStopDistance != 8)
        {
            throw FreezeSplit("SPLIT requires its certified native mode1 adapters, actual motion authority and retained native WORD474");
        }
        HashSet<int> pushContacts = [];
        NativeScriptDirectionResult direction;
        try
        {
            // 525560 preserves474/610; use the same current physical words
            // as the ordinary mode1 direction/stop consumer.
            direction = PrepareNativeDirectionStep(new NativeScriptDirectionQuery(actor.ObjectId, target.FixedX, target.FixedY, target.TargetX, target.TargetY, (short)target.Speed, 8, (ushort)actor.PushRadius, authority.MovementDirection, (byte)actor.Direction, actor.NativeTurnAccumulator474!.Value, actor.MovementDirectionLimit, authority.FacingHold != 0));
        }
        catch (NotSupportedException exception)
        {
            throw FreezeSplit(exception.Message);
        }
        ushort completion = 2;
        PartyJoinTarget next = target with { FixedX = direction.FixedX, FixedY = direction.FixedY };
        if (direction.Status == NativeScriptDirectionStatus.Moving)
        {
            NativeJoinMovementQuery nativeQuery = new(actor.ObjectId, actor.WalkmeshTriangle, target.FixedX, target.FixedY, target.FixedZ, direction.Direction, (short)target.Speed, actor.PushRadius, probe => EvaluateNativePlayerGroundContacts(actor, probe).Collision);
            NativeJoinMovementResult movement = ResolveNativeJoinMovement(nativeQuery) ?? throw FreezeSplit("Unsupported native SPLIT movement geometry; preserved raw coordinates and SPLIT PC require the certified adapter");
            if (movement.Proposals is null || movement.Proposals.Count is < 1 or > 16 || (uint)movement.Direction > 255 || movement.Triangle is < 0 or > short.MaxValue || new[] { movement.FixedX, movement.FixedY, movement.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || movement.Proposals.Any(proposal => (uint)proposal.Direction > 255) || movement.Proposals[0].Direction != direction.Direction || movement.Accepted && (movement.FixedX != movement.Proposals[^1].FixedX || movement.FixedY != movement.Proposals[^1].FixedY || movement.Direction != movement.Proposals[^1].Direction || (movement.FixedZ & 4095) != 0) || !movement.Accepted && (movement.FixedX != target.FixedX || movement.FixedY != target.FixedY || movement.FixedZ != target.FixedZ))
            {
                throw FreezeSplit("Native SPLIT movement adapter returned an inconsistent raw proposal, phase or result");
            }
            try
            {
                if (movement.ProposalGroundProbes is not null)
                {
                    if (movement.ProposalGroundProbes.Count != movement.Proposals.Count)
                    {
                        throw new NotSupportedException("Native SPLIT ground/contact fans do not match all ordered proposals.");
                    }
                    for (int proposal = 0; proposal < movement.Proposals.Count; proposal++)
                    {
                        pushContacts.UnionWith(ValidateNativePlayerGroundContacts(actor, movement.Proposals[proposal], movement.ProposalGroundProbes[proposal]));
                    }
                    if (movement.Accepted && movement.ProposalGroundProbes[^1].Any(probe => probe.ActorCollision))
                    {
                        throw new NotSupportedException("Accepted native SPLIT result retains a blocking actor fan.");
                    }
                }
                else
                {
                    throw new NotSupportedException("Native party movement requires all ordered original fan ground heights.");
                }
            }
            catch (NotSupportedException exception)
            {
                throw FreezeSplit(exception.Message);
            }
            next = target with { FixedX = movement.FixedX, FixedY = movement.FixedY, FixedZ = movement.FixedZ };
            completion = 1;
            actor.WalkmeshTriangle = movement.Triangle;
            if (authority.FacingHold == 0)
            {
                actor.Direction = movement.Direction;
            }
            authority = authority with { MovementDirection = (byte)movement.Direction };
        }
        actor.InvalidateNativeMotionAuthority();
        actor.NativeMovementCompletion542 = completion;
        actor.NativeTurnAccumulator474 = direction.TurnAccumulator;
        actor.NativeMotionAuthority = authority;
        actor.X = next.FixedX / 4096f;
        actor.Y = next.FixedY / 4096f;
        actor.Z = next.FixedZ / 4096f;
        PartyJoinTarget[] targets = (PartyJoinTarget[])partySplit.Targets.Clone();
        targets[index] = next;
        partySplit = partySplit with { Targets = targets };
        if (IsNativePartyPlayerBody(actor))
        {
            Scene.NativePlayerPosition = new FieldFixedPosition(next.FixedX, next.FixedY, next.FixedZ);
        }
        foreach (int objectId in pushContacts)
        {
            actors[objectId].NativePushContactPending = true;
        }
        return true;
    }

    private EventScriptException FreezeSplit(string reason)
    {
        Fiber fiber = fibers.Single(item => item.Sequence == partySplit!.FiberSequence);
        string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: {reason}.";
        LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "SPLIT", message);
        return new EventScriptException(message);
    }
}
