namespace Memoria.FF8.Events;

public sealed record PartySplitState(long FiberSequence, int CallerObjectId, PartyJoinTarget[] Targets, bool UsesNativeMovement = false);

public sealed partial class EventEngine
{
    private PartySplitState? partySplit;

    private FieldFixedPosition? ReadNativeSplitPosition(ActorState actor)
    {
        if (ResolvePartyActor(0)?.ObjectId == actor.ObjectId)
        {
            return ReadNativeJoinLeaderPosition(actor);
        }
        // Below4096, single precision retains every fixed12 XY bit. Larger
        // fractional displays may have lost original low bits and are refused.
        if (!float.IsFinite(actor.X) || !float.IsFinite(actor.Y) || !float.IsFinite(actor.Z) || Math.Abs(actor.X) >= 4096 || Math.Abs(actor.Y) >= 4096 || actor.X * 4096 != MathF.Truncate(actor.X * 4096) || actor.Y * 4096 != MathF.Truncate(actor.Y * 4096) || actor.Z != MathF.Truncate(actor.Z) || actor.Z is < short.MinValue or > short.MaxValue)
        {
            return null;
        }
        return new FieldFixedPosition((int)(actor.X * 4096), (int)(actor.Y * 4096), (int)actor.Z * 4096);
    }

    private bool CanUseNativeSplitMovement(int callerObjectId)
    {
        ActorState[] targets = Enumerable.Range(0, 3).Select(ResolvePartyActor).OfType<ActorState>().ToArray();
        bool solo = targets.Length == 1;
        bool fresh = Scene.NativeFieldInitialization?.IsFrom(NativeRuntimeProfile, FieldId, programHash) == true && Scene.NativeFieldInitialization.Stage == Memoria.FF8.Runtime.NativeFieldInitializationStage.Completed;
        bool legacySolo = solo && Scene.NativeActorObjectIds.All(objectId => objectId == callerObjectId || actors[objectId].ModelId < 0);
        return targets.Length != 0 && targets[0].ObjectId == callerObjectId && (fresh || legacySolo) && ResolveNativeJoinMovement is not null && targets.All(actor => CanUseNativeJoinMovement?.Invoke(actor) == true && (solo || !actor.CollisionEnabled));
    }

    private string? UnsupportedSplitContext(Fiber fiber)
    {
        if (partySplit is not null)
        {
            return LastFault is not null ? "Saved unsupported native SPLIT substep requires its original pre-SPLIT source" : partySplit.FiberSequence == fiber.Sequence ? null : "Concurrent SPLIT would replace original global party movement targets";
        }
        bool usesNativeMovement = CanUseNativeSplitMovement(fiber.ObjectId);
        bool solo = ResolvePartyActor(1) is null && ResolvePartyActor(2) is null;
        if (partyJoin is not null || Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || ResolvePartyActor(0)?.ObjectId != fiber.ObjectId || IsUserControlEnabled || !usesNativeMovement && ResolvePartyJoinStep is null)
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
            if (ReadNativeSplitPosition(actor) is null || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.PushRadius is < 0 or > 200 || !usesNativeMovement && actor.Z != args[6 - slot * 3])
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
                RegisterLegacyBaseAnimations(actor);
                actor.MovementSpeed = speed;
                actor.MovementStopDistance = 8;
                SelectFullNativeAnimation(actor, walk ? actor.WalkAnimationId : actor.RunAnimationId, true);
                AdvanceLaterSplitActorAnimation(actor, fiber.ObjectId);
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
                AdvanceLaterSplitActorAnimation(actor, fiber.ObjectId);
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
        partySplit = null;
    }

    private void AdvanceLaterSplitActorAnimation(ActorState actor, int callerObjectId)
    {
        // TODO(TD-006): remove this compensation when animation and VM execute in native actor order
        // Original529FF0 advances animation before executing each dense actor's
        // VM. A later target advances the newly selected animation this update;
        // the caller had already advanced before its SPLIT reset.
        if (Scene.NativeActorObjectIds.IndexOf(actor.ObjectId) > Scene.NativeActorObjectIds.IndexOf(callerObjectId))
        {
            actor.StepAnimation();
        }
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
            if (partySplit.UsesNativeMovement)
            {
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
                        foreach (NativeJoinMovementProposal proposal in movement.Proposals)
                        {
                            pushContacts.UnionWith(EnsureNativeActorEnvelopeClear(actor, proposal.FixedX, proposal.FixedY, target.FixedZ, proposal.Direction, true));
                        }
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
            int proposalX = unchecked(target.FixedX + NativeSine(direction) * target.Speed / 256);
            int proposalY = unchecked(target.FixedY - NativeCosine(direction) * target.Speed / 256);
            try
            {
                pushContacts.UnionWith(EnsureNativeActorEnvelopeClear(actor, proposalX, proposalY, target.FixedZ, direction, actor.ObjectId == partySplit.CallerObjectId));
            }
            catch (NotSupportedException exception)
            {
                throw FreezeSplit(exception.Message);
            }
            PartyJoinStepQuery query = new(actor.WalkmeshTriangle, proposalX, proposalY, target.FixedZ, actor.PushRadius, direction);
            WalkmeshPosition position = ResolvePartyJoinStep?.Invoke(query) ?? throw FreezeSplit("Native SPLIT step requires slope/wall or blocked geometry evidence; all current positions preserved");
            if (position.Position.Z != target.FixedZ / 4096f || Math.Abs(position.Position.X - proposalX / 4096f) > 0.001f || Math.Abs(position.Position.Y - proposalY / 4096f) > 0.001f)
            {
                throw FreezeSplit("Native SPLIT geometry changed the fixed proposal or reached flat height");
            }
            positions[index] = position;
            directions[index] = direction;
            next[index] = target with { FixedX = proposalX, FixedY = proposalY, Completion = 1 };
        }
        for (int index = 0; index < next.Length; index++)
        {
            ActorState actor = actors[next[index].ObjectId];
            actor.InvalidateNativeMotionAuthority();
            actor.X = next[index].FixedX / 4096f;
            actor.Y = next[index].FixedY / 4096f;
            actor.Z = next[index].FixedZ / 4096f;
            if (positions[index] is WalkmeshPosition position)
            {
                actor.WalkmeshTriangle = position.Triangle;
                actor.Direction = directions[index];
            }
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
