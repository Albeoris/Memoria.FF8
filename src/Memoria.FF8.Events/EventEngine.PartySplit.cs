namespace Memoria.FF8.Events;

public sealed record PartySplitState(long FiberSequence, int CallerObjectId, PartyJoinTarget[] Targets, bool UsesNativeMovement = false);

public sealed partial class EventEngine
{
    private PartySplitState? partySplit;

    private FieldFixedPosition? ReadNativeSplitPosition(ActorState actor)
    {
        return ReadNativeScriptPosition(actor);
    }

    private bool CanUseNativeSplitMovement(int callerObjectId)
    {
        ActorState[] targets = Enumerable.Range(0, 3).Select(ResolvePartyActor).OfType<ActorState>().ToArray();
        bool solo = targets.Length == 1;
        bool fresh = Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, FieldId, programHash) == true && Scene.NativeFieldInitialization.Stage == Memoria.FF8.Runtime.NativeFieldInitializationStage.Completed;
        return targets.Length != 0 && targets[0].ObjectId == callerObjectId && fresh && ResolveNativeJoinMovement is not null && targets.All(actor => CanUseNativeJoinMovement?.Invoke(actor) == true && (solo || !actor.CollisionEnabled));
    }

    private string? UnsupportedSplitContext(Fiber fiber)
    {
        if (partySplit is not null)
        {
            return LastFault is not null ? "Saved unsupported native SPLIT substep requires its original pre-SPLIT source" : partySplit.FiberSequence == fiber.Sequence ? null : "Concurrent SPLIT would replace original global party movement targets";
        }
        bool usesNativeMovement = CanUseNativeSplitMovement(fiber.ObjectId);
        bool solo = ResolvePartyActor(1) is null && ResolvePartyActor(2) is null;
        if (partyJoin is not null || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || ResolvePartyActor(0)?.ObjectId != fiber.ObjectId || IsUserControlEnabled || !usesNativeMovement)
        {
            return "Native SPLIT requires the reached compact leader/disabled-control context and bounded flat movement service";
        }
        int[] args = fiber.Stack.ToArray();
        if (args.Length < 9 || args.Take(9).Any(value => value is < short.MinValue or > short.MaxValue))
        {
            return "Native SPLIT requires nine original signed field-coordinate operands";
        }
        for (int slot = 0; slot < 3; slot++)
        {
            if (ResolvePartyActor(slot) is not ActorState actor)
            {
                continue;
            }
            if (actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 } || ReadNativeSplitPosition(actor) is null || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.PushRadius is < 0 or > ushort.MaxValue)
            {
                return "Native SPLIT requires proved fixed actor positions, completed prior motion and the reached flat target height";
            }
            if (!solo && actor.CollisionEnabled)
            {
                return "Native SPLIT atomic preflight requires the reached THROUGHON moving party peers; enabled moving-peer contact needs native staged ordering";
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
            bool usesNativeMovement = CanUseNativeSplitMovement(fiber.ObjectId);
            int[] args = Pop(fiber, 9);
            List<PartyJoinTarget> targets = [];
            for (int slot = 0; slot < 3; slot++)
            {
                if (ResolvePartyActor(slot) is not ActorState actor)
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
                SelectFullNativeAnimation(actor, walk ? actor.WalkAnimationId : actor.RunAnimationId, true);
                targets.Add(new PartyJoinTarget(actor.ObjectId, raw.X, raw.Y, raw.Z, targetX, targetY, targetZ, speed, 0));
            }
            partySplit = new PartySplitState(fiber.Sequence, fiber.ObjectId, targets.ToArray(), usesNativeMovement);
        }
        foreach (PartyJoinTarget target in partySplit.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            if (target.Completion == 2 && actor.AnimationId != actor.BaseAnimationId)
            {
                SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
            }
        }
        if (partySplit.Targets.Any(target => target.Completion != 2))
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

    private void StepSplitParty()
    {
        if (partySplit is null)
        {
            return;
        }
        if (partySplit.UsesNativeMovement && !CanUseNativeSplitMovement(partySplit.CallerObjectId))
        {
            throw FreezeSplit("Saved native SPLIT requires its certified party geometry callback and moving-peer flags; flat fallback is unsupported");
        }
        PartyJoinTarget[] next = new PartyJoinTarget[partySplit.Targets.Length];
        WalkmeshPosition?[] positions = new WalkmeshPosition?[next.Length];
        int[] directions = new int[next.Length];
        HashSet<int> pushContacts = [];
        for (int index = 0; index < next.Length; index++)
        {
            PartyJoinTarget target = partySplit.Targets[index];
            ActorState actor = actors[target.ObjectId];
            next[index] = target;
            if (target.Completion == 2)
            {
                continue;
            }
            int x = target.FixedX >> 12;
            int y = target.FixedY >> 12;
            int dx = (target.TargetX >> 12) - x;
            int dy = (target.TargetY >> 12) - y;
            int distanceSquared = unchecked(dx * dx + dy * dy);
            int stop = actor.PushRadius + 8;
            if (distanceSquared <= stop * stop + 4096)
            {
                next[index] = target with { Completion = 2 };
                continue;
            }
            if (distanceSquared < (target.Speed * target.Speed >> 16) || distanceSquared < 4)
            {
                next[index] = target with { FixedX = target.TargetX, FixedY = target.TargetY, Completion = 2 };
                continue;
            }
            int direction = NativePartyDirection(x, y, target.TargetX >> 12, target.TargetY >> 12);
            NativeJoinMovementQuery nativeQuery = new(actor.ObjectId, actor.WalkmeshTriangle, target.FixedX, target.FixedY, target.FixedZ, direction, (short)target.Speed, actor.PushRadius, probe => EvaluateNativePlayerGroundContacts(actor, probe).Collision);
            NativeJoinMovementResult movement = ResolveNativeJoinMovement?.Invoke(nativeQuery) ?? throw FreezeSplit("Native SPLIT geometry callback refused the original scripted step");
            if (movement.Proposals.Count is < 1 or > 16 || movement.Direction is < 0 or > 255 || movement.Triangle is < 0 or > short.MaxValue || movement.Proposals.Any(proposal => proposal.Direction is < 0 or > 255 || (proposal.FixedX >> 12) is < short.MinValue or > short.MaxValue || (proposal.FixedY >> 12) is < short.MinValue or > short.MaxValue) || new[] { movement.FixedX, movement.FixedY, movement.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || (movement.FixedZ & 4095) != 0 || movement.Accepted && (movement.FixedX != movement.Proposals[^1].FixedX || movement.FixedY != movement.Proposals[^1].FixedY) || !movement.Accepted && (movement.FixedX != target.FixedX || movement.FixedY != target.FixedY || movement.FixedZ != target.FixedZ))
            {
                throw FreezeSplit("Native SPLIT geometry returned invalid fixed coordinates, proposal order or blocked placement");
            }
            try
            {
                if (movement.ProposalGroundProbes is not null)
                {
                    if (movement.ProposalGroundProbes.Count != movement.Proposals.Count)
                    {
                        throw new NotSupportedException("Native SPLIT ground/contact fans do not match all original retry proposals.");
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
            positions[index] = new WalkmeshPosition(new System.Numerics.Vector3(movement.FixedX / 4096f, movement.FixedY / 4096f, movement.FixedZ / 4096f), movement.Triangle);
            directions[index] = movement.Direction;
            next[index] = target with { FixedX = movement.FixedX, FixedY = movement.FixedY, FixedZ = movement.FixedZ, Completion = 1 };
            continue;
        }
        for (int index = 0; index < next.Length; index++)
        {
            ActorState actor = actors[next[index].ObjectId];
            NativeMotionAuthority authority = actor.NativeMotionAuthority ?? throw FreezeSplit("SPLIT requires preserved native movement575 and facing hold576");
            actor.InvalidateNativeMotionAuthority();
            actor.X = next[index].FixedX / 4096f;
            actor.Y = next[index].FixedY / 4096f;
            actor.Z = next[index].FixedZ / 4096f;
            if (positions[index] is WalkmeshPosition position)
            {
                actor.WalkmeshTriangle = position.Triangle;
                actor.Direction = authority.FacingHold == 1 ? actor.Direction : directions[index];
            }
            // Native mode1 changes575 and changes577 only when576 is clear.
            actor.NativeMotionAuthority = authority with { MovementDirection = positions[index] is null ? authority.MovementDirection : (byte)directions[index] };
            if (actor.ObjectId == partySplit.CallerObjectId)
            {
                Scene.NativePlayerPosition = new FieldFixedPosition(next[index].FixedX, next[index].FixedY, next[index].FixedZ);
            }
        }
        foreach (int objectId in pushContacts)
        {
            actors[objectId].NativePushContactPending = true;
        }
        partySplit = partySplit with { Targets = next };
    }

    private EventScriptException FreezeSplit(string reason)
    {
        Fiber fiber = fibers.Single(item => item.Sequence == partySplit!.FiberSequence);
        string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: {reason}.";
        LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "SPLIT", message);
        return new EventScriptException(message);
    }
}
