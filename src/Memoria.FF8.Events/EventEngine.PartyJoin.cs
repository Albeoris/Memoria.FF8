using System.Numerics;

namespace Memoria.FF8.Events;

public sealed record NativeJoinMovementQuery(int ObjectId, int Triangle, int FixedX, int FixedY, int FixedZ, int Direction, short Speed, int Radius, Func<NativePlayerGroundProbe, bool>? HasActorCollision = null);
public sealed record NativeJoinMovementProposal(int FixedX, int FixedY, int Direction);
public sealed record NativeJoinMovementResult(bool Accepted, int FixedX, int FixedY, int FixedZ, int Triangle, int Direction, IReadOnlyList<NativeJoinMovementProposal> Proposals, IReadOnlyList<IReadOnlyList<NativePlayerGroundProbe>>? ProposalGroundProbes = null);
public sealed record PartyJoinTarget(int ObjectId, int FixedX, int FixedY, int FixedZ, int TargetX, int TargetY, int TargetZ, int Speed);
public sealed record PartyJoinState(long FiberSequence, int CallerObjectId, PartyJoinTarget[] Targets);
public sealed record NativeJoinedPosition(int Version, int FieldId, int CallerObjectId, int ScriptId, int Operation, long FiberSequence, long Frame, int FixedX, int FixedY, int FixedZ, int Triangle, NativeMotionAuthority Authority);

public sealed partial class EventEngine
{
    private PartyJoinState? partyJoin;
    public Func<int, int, int, int?>? ResolvePartyTraceTriangle { get; set; }
    public Func<NativeJoinMovementQuery, NativeJoinMovementResult?>? ResolveNativeJoinMovement { get; set; }
    public Func<ActorState, bool>? CanUseNativeJoinMovement { get; set; }

    private ActorState? ResolveJoinPartyActor(int slot)
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("JOIN requires authenticated original runtime provenance.");
        }
        if (Scene.NativeActorLayoutVersion != 1 || Scene.PartyBindingMemory is not { Version: 1 } memory)
        {
            throw new NotSupportedException("Native JOIN requires its original saved party body bindings.");
        }
        int nativeActor = memory.At(slot);
        if (variables.TryGetValue(194 + slot, out int alias) && alias != nativeActor || nativeActor != byte.MaxValue && (uint)nativeActor >= Scene.NativeActorObjectIds.Count)
        {
            throw new NotSupportedException("Native JOIN saved party binding differs from its represented body table.");
        }
        // 524BA0 and47ACE0 read savedB194..196, independently of the
        // runtime1CE4772 follow mappings changed by FOLLOWOFF/FOLLOWON.
        return nativeActor == byte.MaxValue ? null : actors[Scene.NativeActorObjectIds[nativeActor]];
    }

    // Original signed-WORD quarter-wave at B80D1C. The full native table is
    // reconstructed by its exact quadrant symmetry, without rounded trig.
    private static readonly short[] NativeSineQuarter = [0, 100, 200, 301, 401, 501, 601, 700, 799, 897, 995, 1092, 1189, 1284, 1379, 1474, 1567, 1659, 1751, 1841, 1930, 2018, 2105, 2191, 2275, 2358, 2439, 2519, 2598, 2675, 2750, 2824, 2896, 2966, 3034, 3101, 3166, 3229, 3289, 3348, 3405, 3460, 3513, 3563, 3612, 3658, 3702, 3744, 3784, 3821, 3856, 3889, 3919, 3947, 3973, 3996, 4017, 4035, 4051, 4065, 4076, 4084, 4091, 4094, 4096];

    private static int NativeSine(int direction)
    {
        int angle = (byte)direction;
        int quadrant = angle >> 6;
        int offset = angle & 63;
        int value = NativeSineQuarter[quadrant is 1 or 3 ? 64 - offset : offset];
        return quadrant >= 2 ? -value : value;
    }

    private static int NativeCosine(int direction) => NativeSine(direction + 64);

    // Even byte entries of the original B80C18 direction lookup table.
    private static readonly byte[] NativeDirectionAngles = [0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 20, 20, 20, 21, 21, 21, 22, 22, 22, 23, 23, 24, 24, 24, 25, 25, 26, 26, 26, 27, 27, 28, 28, 28, 29, 29, 30, 30, 30, 31, 31, 32];

    private static int NativePartyDirection(int fromX, int fromY, int toX, int toY)
    {
        int dx = unchecked(toX - fromX);
        int dy = unchecked(toY - fromY);
        int distance = (int)Math.Sqrt(unchecked(dx * dx + dy * dy));
        int x = distance == 0 ? 0 : unchecked(dx << 12) / distance / 32;
        int y = distance == 0 ? 0 : unchecked(dy << 12) / distance / 32;
        bool horizontal = unchecked(x * x) > unchecked(y * y);
        int minor = Math.Abs(horizontal ? y : x);
        if ((uint)minor > 128)
        {
            throw new NotSupportedException("Native party direction leaves the proved original table range.");
        }
        // Original entries90..128 all contain32, including short-vector
        // normalization where the truncated integer length expands the ratio.
        int angle = NativeDirectionAngles[Math.Min(minor, 90)];
        return (byte)(horizontal ? x > 0 ? 64 + Math.Sign(y) * angle : 192 - Math.Sign(y) * angle : y > 0 ? 128 - Math.Sign(x) * angle : Math.Sign(x) * angle);
    }

    private static bool TryReadNativeBaseAnimations(ActorState actor, out int idle, out int walk, out int run)
    {
        idle = actor.BaseAnimationId;
        walk = actor.WalkAnimationId;
        run = actor.RunAnimationId;
        if (actor.NativeBaseAnimationVersion != 1)
        {
            return false;
        }
        return idle is >= 0 and <= 255 && walk is >= 0 and <= 255 && run is >= 0 and <= 255;
    }

    private string? UnsupportedJoinContext(Fiber fiber)
    {
        if (partyJoin is not null)
        {
            if (LastFault is not null)
            {
                return "Saved unsupported native JOIN substep cannot resume from this snapshot";
            }
            return partyJoin.FiberSequence == fiber.Sequence ? null : "Unsupported concurrent JOIN would replace the native global companion targets";
        }
        if (Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || ResolveJoinPartyActor(0)?.ObjectId != fiber.ObjectId || Scene.Party.Count == 0 || actors[fiber.ObjectId].CharacterId != Scene.Party[0])
        {
            return "Unsupported JOIN requires the proved compact bindings and original party leader caller";
        }
        if (ResolvePartyTraceTriangle is null || ResolveNativeJoinMovement is null || ResolveNativeScriptDirection is null)
        {
            return "Unsupported JOIN requires native trace geometry and shared native direction/movement adapters";
        }
        if (actors[fiber.ObjectId].MovementTarget is not null || actors[fiber.ObjectId].MovementActorId is not null)
        {
            return "Unsupported JOIN requires completed original leader movement before fixing companion targets";
        }
        if (ReadNativeJoinLeaderPosition(actors[fiber.ObjectId]) is null)
        {
            return "Unsupported fractional JOIN leader requires its matching preserved native fixed position";
        }
        if (Enumerable.Range(1, 2).Select(ResolveJoinPartyActor).OfType<ActorState>().Any(actor => CanUseNativeJoinMovement?.Invoke(actor) != true))
        {
            return "JOIN requires the certified native movement adapter for every companion; flat fallback is unsupported";
        }
        foreach (ActorState actor in Enumerable.Range(0, 3).Select(ResolveJoinPartyActor).OfType<ActorState>())
        {
            if (actor.NativeLadderMode3 is { Active: true })
            {
                return "Native JOIN cannot replace an active mode3 companion without its physical overlap contract";
            }
            if (actor.ObjectId != fiber.ObjectId && actor.NativeTurnAccumulator474 is null)
            {
                return "Native JOIN requires the actual preserved companion WORD474 before target issue";
            }
            if (actor.ObjectId != fiber.ObjectId && actor.NativeMotionAuthority is not { Version: 1 })
            {
                return "Native JOIN requires actual companion movement575 and facing hold576 before issue";
            }
            if (!TryReadNativeBaseAnimations(actor, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(actor.RenderModelId, animation) is not > 0))
            {
                return "Unsupported JOIN requires original idle/walk/run IDs and complete model animation metadata";
            }
            if (ReadNativeScriptPosition(actor) is null || actor.PushRadius is < 0 or > ushort.MaxValue)
            {
                return "JOIN requires preserved native raw companion coordinates and unsigned radius";
            }
        }
        return null;
    }

    private FieldFixedPosition? ReadNativeJoinLeaderPosition(ActorState leader)
    {
        if (Scene.NativePlayerPosition is FieldFixedPosition raw && (raw.X >> 12) is >= short.MinValue and <= short.MaxValue && (raw.Y >> 12) is >= short.MinValue and <= short.MaxValue && (raw.Z >> 12) is >= short.MinValue and <= short.MaxValue && leader.X == raw.X / 4096f && leader.Y == raw.Y / 4096f && leader.Z == raw.Z / 4096f)
        {
            return raw;
        }
        if (!float.IsFinite(leader.X) || !float.IsFinite(leader.Y) || !float.IsFinite(leader.Z) || leader.X != MathF.Truncate(leader.X) || leader.Y != MathF.Truncate(leader.Y) || leader.Z != MathF.Truncate(leader.Z) || leader.X is < short.MinValue or > short.MaxValue || leader.Y is < short.MinValue or > short.MaxValue || leader.Z is < short.MinValue or > short.MaxValue)
        {
            return null;
        }
        // Original script MOVE endpoints already proved integral fixed12.
        // A fractional display value alone cannot recover lost raw bits.
        return new FieldFixedPosition((int)leader.X * 4096, (int)leader.Y * 4096, (int)leader.Z * 4096);
    }

    private void ExecuteJoin(Fiber fiber)
    {
        if (partyJoin is null)
        {
            ActorState caller = actors[fiber.ObjectId];
            FieldFixedPosition callerPosition = ReadNativeJoinLeaderPosition(caller)!;
            List<PartyJoinTarget> targets = [];
            for (int slot = 1; slot < 3; slot++)
            {
                if (ResolveJoinPartyActor(slot) is not ActorState target)
                {
                    continue;
                }
                FieldFixedPosition raw = ReadNativeScriptPosition(target)!;
                int fixedX = raw.X;
                int fixedY = raw.Y;
                int fixedZ = raw.Z;
                int dx = unchecked(callerPosition.X - fixedX) / 4096;
                int dy = unchecked(callerPosition.Y - fixedY) / 4096;
                bool walk = unchecked(dx * dx + dy * dy) < 259200;
                int speed = (short)((walk ? 69020 : 203000) * 20 >> 9);
                target.MovementSpeed = speed;
                target.MovementActorId = null;
                target.MovementTarget = null;
                target.MovementStopDistance = 96;
                // 524F40 replaces the physical mode1 target, not VM373 or the
                // request backup. Keep only the displaced MOVE's issued receipt.
                target.NativeScriptMovement = target.NativeScriptMovement is { } previous && (previous.Active || previous.Cancelled || target.NativeMovementRequest is not null) ? previous with { Active = false, Cancelled = true, Suspended = false, SupersededByPartyJoin = true, AnimationFlag40000 = previous.AnimationFlag40000 ?? previous.ResetAnimationOnCompletion ?? previous.AutomaticAnimation } : null;
                target.NativeJoinedPosition = null;
                // Native issue replaces mode572=2 with mode1; its former ballistic phase no longer owns this body.
                target.NativeJump = null;
                target.NativeLadder = null;
                target.NativeLadderMode3 = null;
                target.NativeMovementCompletion542 = 0;
                SelectFullNativeAnimation(target, walk ? target.WalkAnimationId : target.RunAnimationId, true);
                targets.Add(new PartyJoinTarget(target.ObjectId, fixedX, fixedY, fixedZ, callerPosition.X, callerPosition.Y, callerPosition.Z, speed));
            }
            SelectFullNativeAnimation(caller, caller.BaseAnimationId, true);
            partyJoin = new PartyJoinState(fiber.Sequence, caller.ObjectId, targets.ToArray());
            fiber.Position--;
            fiber.WaitReason = WaitReason.PartyJoin;
            return;
        }
        bool completed = partyJoin.Targets.All(target => actors[target.ObjectId].NativeMovementCompletion542 == 2);
        (PartyTraceState Trace, FieldFixedPosition LeaderPosition)? preparedTrace = null;
        if (completed)
        {
            try
            {
                preparedTrace = PrepareJoinedPartyTrace();
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or InvalidDataException or ArithmeticException or NotSupportedException)
            {
                // Run has advanced this held JOIN before dispatch. Post-VM JOIN
                // movement faults use FreezeJoin without this command rewind.
                fiber.Position--;
                fiber.WaitReason = WaitReason.PartyJoin;
                throw FreezeJoin(exception.Message);
            }
        }
        foreach (PartyJoinTarget target in partyJoin.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            if (actors[target.ObjectId].NativeMovementCompletion542 == 2 && actor.NativeAnimationHeader?.AnimationId != actor.BaseAnimationId)
            {
                SelectNativeBodyAnimation(actor, unchecked((byte)actor.BaseAnimationId), AnimationHold);
            }
        }
        if (!completed)
        {
            fiber.Position--;
            fiber.WaitReason = WaitReason.PartyJoin;
            return;
        }
        Scene.PartyTrace = preparedTrace!.Value.Trace;
        Scene.NativePlayerPosition = preparedTrace.Value.LeaderPosition;
        Scene.NativePartyTraceVersion = 1;
        foreach (PartyJoinTarget target in partyJoin.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            actor.MovementStopDistance = 0;
            if (NativeRuntimeProfile is { IsVerified: true })
            {
                actor.NativeJoinedPosition = new NativeJoinedPosition(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, Frame, target.FixedX, target.FixedY, target.FixedZ, actor.WalkmeshTriangle, actor.NativeMotionAuthority!);
            }
        }
        partyJoin = null;
    }

    private void SelectFullNativeAnimation(ActorState actor, int animation, bool reset, bool bodyHeader = true)
    {
        if (bodyHeader && NativeRuntimeProfile is { IsVerified: true })
        {
            SelectNativeBodyAnimation(actor, unchecked((byte)animation), AnimationRepeating);
            return;
        }
        if (HasNativeEmptyAnimationModel(actor))
        {
            // 532A40 routes native536=-1 through531D40, which retains an
            // empty model record.532890(mode13) does not select a resource.
            return;
        }
        int frames = AnimationFrameCount?.Invoke(actor.RenderModelId, animation) ?? throw new NotSupportedException("Native animation selection requires model frame metadata.");
        if (frames <= 0)
        {
            throw new NotSupportedException("Native animation has no decoded frames.");
        }
        actor.AnimationId = animation;
        actor.AnimationFirst = 0;
        actor.AnimationLast = frames - 1;
        if (reset)
        {
            actor.AnimationFrame = 0;
        }
        actor.AnimationKeep = false;
        actor.AnimationLoop = true;
        actor.AnimationPlaying = true;
        if (animation == actor.BaseAnimationId)
        {
            actor.BaseFirstFrame = 0;
            actor.BaseLastFrame = frames - 1;
        }
    }

    private bool HasNativeEmptyAnimationModel(ActorState actor)
    {
        return NativeRuntimeProfile is { IsVerified: true } && Scene.NativeActorLayoutVersion == 1 && Scene.NativeActorObjectIds.Contains(actor.ObjectId) && actor.RenderModelId is -1 or ushort.MaxValue;
    }

    private bool StepNativeJoinedCompanion(ActorState actor)
    {
        int index = partyJoin is null ? -1 : Array.FindIndex(partyJoin.Targets, target => target.ObjectId == actor.ObjectId);
        if (index < 0)
        {
            return false;
        }
        PartyJoinTarget target = partyJoin!.Targets[index];
        if (actors[target.ObjectId].NativeMovementCompletion542 == 2)
        {
            return true;
        }
        if (ResolveNativeJoinMovement is null || ResolveNativeScriptDirection is null || CanUseNativeJoinMovement?.Invoke(actor) != true || actor.NativeMotionAuthority is not { Version: 1 } authority || actor.NativeTurnAccumulator474 is null || (uint)actor.Direction > byte.MaxValue || actor.PushRadius is < 0 or > ushort.MaxValue || actor.MovementSpeed != target.Speed || actor.MovementStopDistance != 96)
        {
            throw FreezeJoin("JOIN requires its certified native mode1 adapters, actual motion authority and retained native WORD474");
        }
        NativeScriptDirectionResult direction;
        try
        {
            // 524F40 preserves474/610. The shared mode1 helper consumes the
            // current physical word and limit, including completed prior MOVE state.
            direction = PrepareNativeDirectionStep(new NativeScriptDirectionQuery(actor.ObjectId, target.FixedX, target.FixedY, target.TargetX, target.TargetY, (short)target.Speed, 96, (ushort)actor.PushRadius, authority.MovementDirection, (byte)actor.Direction, actor.NativeTurnAccumulator474!.Value, actor.MovementDirectionLimit, authority.FacingHold != 0));
        }
        catch (NotSupportedException exception)
        {
            throw FreezeJoin(exception.Message);
        }
        ushort completion = 2;
        PartyJoinTarget next = target with { FixedX = direction.FixedX, FixedY = direction.FixedY };
        if (direction.Status == NativeScriptDirectionStatus.Moving)
        {
            NativeJoinMovementQuery nativeQuery = new(actor.ObjectId, actor.WalkmeshTriangle, target.FixedX, target.FixedY, target.FixedZ, direction.Direction, (short)target.Speed, actor.PushRadius, probe => EvaluateNativePlayerGroundContacts(actor, probe).Collision);
            NativeJoinMovementResult movement = ResolveNativeJoinMovement(nativeQuery) ?? throw FreezeJoin("Unsupported native JOIN movement geometry; preserved raw coordinates and JOIN PC require the certified adapter");
            if (movement.Proposals is null || movement.Proposals.Count is < 1 or > 16 || (uint)movement.Direction > 255 || movement.Triangle is < 0 or > short.MaxValue || new[] { movement.FixedX, movement.FixedY, movement.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || movement.Proposals.Any(proposal => (uint)proposal.Direction > 255) || movement.Proposals[0].Direction != direction.Direction || movement.Accepted && (movement.FixedX != movement.Proposals[^1].FixedX || movement.FixedY != movement.Proposals[^1].FixedY || movement.Direction != movement.Proposals[^1].Direction || (movement.FixedZ & 4095) != 0) || !movement.Accepted && (movement.FixedX != target.FixedX || movement.FixedY != target.FixedY || movement.FixedZ != target.FixedZ))
            {
                throw FreezeJoin("Native JOIN movement adapter returned an inconsistent raw proposal, phase or result");
            }
            try
            {
                if (movement.ProposalGroundProbes is not null)
                {
                    if (movement.ProposalGroundProbes.Count != movement.Proposals.Count)
                    {
                        throw new NotSupportedException("Native JOIN ground/contact fans do not match all ordered proposals.");
                    }
                    for (int proposal = 0; proposal < movement.Proposals.Count; proposal++)
                    {
                        _ = ValidateNativePlayerGroundContacts(actor, movement.Proposals[proposal], movement.ProposalGroundProbes[proposal]);
                    }
                    if (movement.Accepted && movement.ProposalGroundProbes[^1].Any(probe => probe.ActorCollision))
                    {
                        throw new NotSupportedException("Accepted native JOIN result retains a blocking actor fan.");
                    }
                }
                else
                {
                    throw new NotSupportedException("Native party movement requires all ordered original fan ground heights.");
                }
            }
            catch (NotSupportedException exception)
            {
                throw FreezeJoin(exception.Message);
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
        NativeScriptMovementState? displaced = actor.NativeScriptMovement is { SupersededByPartyJoin: true } previous ? previous with { FixedX = next.FixedX, FixedY = next.FixedY, FixedZ = next.FixedZ, Triangle = actor.WalkmeshTriangle, MovementDirection = authority.MovementDirection, FacingDirection = (byte)actor.Direction, FacingHold576 = authority.FacingHold } : null;
        actor.InvalidateNativeMotionAuthority();
        actor.NativeScriptMovement = displaced;
        actor.NativeMovementCompletion542 = completion;
        actor.NativeTurnAccumulator474 = direction.TurnAccumulator;
        actor.NativeMotionAuthority = authority;
        actor.X = next.FixedX / 4096f;
        actor.Y = next.FixedY / 4096f;
        actor.Z = next.FixedZ / 4096f;
        PartyJoinTarget[] targets = (PartyJoinTarget[])partyJoin.Targets.Clone();
        targets[index] = next;
        partyJoin = partyJoin with { Targets = targets };
        return true;
    }

    private EventScriptException FreezeJoin(string reason)
    {
        Fiber fiber = fibers.Single(item => item.Sequence == partyJoin!.FiberSequence);
        string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: {reason}.";
        LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "JOIN", message);
        return new EventScriptException(message);
    }

    private void ValidateNativeJoinedPositions(Checkpoint saved)
    {
        foreach (ActorState actor in saved.Actors.Where(actor => actor.NativeScriptMovement is { SupersededByPartyJoin: true }))
        {
            NativeScriptMovementState displaced = actor.NativeScriptMovement!;
            PartyJoinTarget? active = saved.PartyJoin?.Targets.SingleOrDefault(target => target.ObjectId == actor.ObjectId);
            NativeJoinedPosition? completed = actor.NativeJoinedPosition;
            bool activeOwner = active is not null && active.FixedX == displaced.FixedX && active.FixedY == displaced.FixedY && active.FixedZ == displaced.FixedZ;
            bool completedOwner = completed is not null && completed.FixedX == displaced.FixedX && completed.FixedY == displaced.FixedY && completed.FixedZ == displaced.FixedZ && actor.NativeMovementCompletion542 == 2;
            if (displaced.Active || !displaced.Cancelled && !(displaced.Asynchronous && actor.NativeMovementCompletion542 == 2 && actor.NativeMovementRequest is not null) || displaced.Suspended || displaced.AnimationFlag40000 is null || !activeOwner && !completedOwner)
            {
                throw new InvalidDataException("Saved displaced MOVE lost its active or completed native JOIN physical owner.");
            }
        }
        foreach (ActorState actor in saved.Actors.Where(actor => actor.NativeJoinedPosition is not null || actor.NativeSplitPosition is not null))
        {
            NativeJoinedPosition joined = actor.NativeJoinedPosition ?? actor.NativeSplitPosition!;
            Albeoris.Games.FF8.Jsm.Jsm.Opcode opcode = actor.NativeJoinedPosition is not null ? Albeoris.Games.FF8.Jsm.Jsm.Opcode.JOIN : Albeoris.Games.FF8.Jsm.Jsm.Opcode.SPLIT;
            EventScript? script = (uint)joined.CallerObjectId < program!.Objects.Count ? program.Objects[joined.CallerObjectId].Scripts.FirstOrDefault(script => script.Id == joined.ScriptId) : null;
            if (NativeRuntimeProfile is not { IsVerified: true } || joined.Version != 1 || joined.FieldId != saved.FieldId || joined.Frame is < 1 || joined.Frame > saved.Frame || joined.FiberSequence < 1 || script is null || !script.ContainsOperation(joined.Operation) || program.Operations[joined.Operation].Opcode != opcode || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.JOIN && actor.ObjectId == joined.CallerObjectId || joined.Authority is not { Version: 1 } || actor.NativeMotionAuthority != joined.Authority || actor.NativeScriptMovement is not null && !(opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.JOIN && actor.NativeScriptMovement.SupersededByPartyJoin) || actor.NativeJump is not null || actor.X != joined.FixedX / 4096f || actor.Y != joined.FixedY / 4096f || actor.Z != joined.FixedZ / 4096f || actor.WalkmeshTriangle != joined.Triangle || joined.Triangle is < 0 or > short.MaxValue || (joined.FixedZ & 4095) != 0 || new[] { joined.FixedX, joined.FixedY, joined.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
            {
                throw new InvalidDataException("Saved completed JOIN body lost its original instruction, raw position or physical motion authority.");
            }
        }
    }

    private (PartyTraceState Trace, FieldFixedPosition LeaderPosition) PrepareJoinedPartyTrace()
    {
        ActorState leader = ResolveJoinPartyActor(0)!;
        FieldFixedPosition leaderPosition = ReadNativeJoinLeaderPosition(leader) ?? throw new NotSupportedException("Native JOIN leader lost its preserved raw position");
        PartyTraceSample?[] first = ResetTraceFlags(Scene.PartyTrace?.FirstTrail);
        PartyTraceSample?[] second = ResetTraceFlags(Scene.PartyTrace?.SecondTrail);
        int? triangleWinner = null;
        BuildJoinedTrail(first, ResolveJoinPartyActor(1), ref triangleWinner);
        BuildJoinedTrail(second, ResolveJoinPartyActor(2), ref triangleWinner);
        return (new PartyTraceState(0, 15, 30, first, second), leaderPosition);
    }

    private static PartyTraceSample?[] ResetTraceFlags(PartyTraceSample?[]? previous)
    {
        return Enumerable.Range(0, 64).Select(index => previous is { Length: 64 } && previous[index] is PartyTraceSample sample ? sample with { FirstFlag = 1, SecondFlag = 1 } : null).ToArray();
    }

    private void BuildJoinedTrail(PartyTraceSample?[] trail, ActorState? follower, ref int? triangleWinner)
    {
        if (follower is null)
        {
            // Native FF coordinates are outside the retained array. Both native
            // position and animation consumers guard FF before reading this ring.
            Array.Clear(trail);
            return;
        }
        ActorState leader = ResolveJoinPartyActor(0)!;
        FieldFixedPosition leaderPosition = ReadNativeJoinLeaderPosition(leader) ?? throw new NotSupportedException("Native JOIN trace leader lost its preserved raw position");
        PartyJoinTarget fixedFollower = partyJoin!.Targets.Single(target => target.ObjectId == follower.ObjectId);
        int followerX = fixedFollower.FixedX / 4096;
        int followerY = fixedFollower.FixedY / 4096;
        int followerZ = fixedFollower.FixedZ / 4096;
        int leaderX = leaderPosition.X / 4096;
        int leaderY = leaderPosition.Y / 4096;
        int leaderZ = leaderPosition.Z / 4096;
        int dx = leaderX - followerX;
        int dy = leaderY - followerY;
        int distance = (int)Math.Sqrt(unchecked(dx * dx + dy * dy));
        int speed = 69020 * 20 >> 9;
        int count = (distance << 8) / speed;
        if (count >= 32)
        {
            return;
        }
        int direction = NativePartyDirection(followerX, followerY, leaderX, leaderY);
        int x = leaderPosition.X;
        int y = leaderPosition.Y;
        for (int step = 0; step < 32; step++)
        {
            int sampleX = step < count ? x / 4096 : followerX;
            int sampleY = step < count ? y / 4096 : followerY;
            int sampleZ = step < count ? leaderZ - step * ((unchecked(leaderPosition.Z - fixedFollower.FixedZ)) / 4096) / count : followerZ;
            int? candidate = ResolvePartyTraceTriangle?.Invoke(sampleX, sampleY, step < count ? leaderZ : followerZ);
            if (candidate is int selected)
            {
                triangleWinner = selected;
            }
            else if (NativeRuntimeProfile is not { IsVerified: true } || triangleWinner is null)
            {
                throw new NotSupportedException("Native JOIN trace triangle lookup has no containing original geometry");
            }
            //47ACE0's four lookup sites share477B00's winner WORD at the same stack address.
            //Only a real hit seeds it; a later miss retains it across both rings, never across JOIN calls.
            int triangle = triangleWinner!.Value;
            trail[63 - step] = new PartyTraceSample((short)sampleX, (short)sampleY, (short)sampleZ, triangle, direction, step < count ? 0 : 2);
            if (step < count)
            {
                x = unchecked(x + speed * NativeSine(direction) / -256);
                y = unchecked(y + speed * NativeCosine(direction) / 256);
            }
        }
    }

    // 4789A0 compensates for the movie callback cadence before narrowing actor WORD510.
    private int NativePlayerMovementSpeed => unchecked((short)((NativePlayerWalking ? 69020 : 203000) * (services.IsMoviePlaying ? 2 : 1) * 20 >> 9));

    private (WalkmeshPosition Position, FieldFixedPosition FixedPosition, int Direction, int[] PushContacts, bool NativeMovement, FieldFixedPosition? TriggerPosition, bool MovementSucceeded) ResolveNativePartyPlayerStep(ActorState player, Vector2 axes)
    {
        FieldFixedPosition current = ReadNativePartyPlayerPosition(player);
        int direction = axes == Vector2.Zero ? (byte)player.Direction : (byte)(NativePartyDirection(0, 0, (int)(axes.X * 4096), (int)(axes.Y * 4096)) + InputAxisDirection);
        int speed = NativePlayerMovementSpeed;
        if (ResolveNativePlayerMovement is null || CanUseNativePlayerMovement?.Invoke(player) != true)
        {
            throw new NotSupportedException("Native party player movement requires the certified native adapter; flat fallback is unsupported.");
        }
        if (axes != Vector2.Zero)
        {
            return ResolveCertifiedNativePlayerStep(player, current, direction, speed);
        }
        ValidatePendingNativePushContacts();
        WalkmeshPosition stationary = new(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle);
        PreparePartyTraceFollowers(stationary, direction, false);
        return (stationary, current, direction, [], false, null, false);
    }

    private FieldFixedPosition ReadNativePartyPlayerPosition(ActorState player)
    {
        FieldFixedPosition position = Scene.NativePlayerPosition ?? throw new NotSupportedException("Native party trace has no preserved fixed player position.");
        if (player.X != position.X / 4096f || player.Y != position.Y / 4096f || player.Z != position.Z / 4096f)
        {
            throw new NotSupportedException("Script movement changed the native party leader's position outside the proved fixed movement driver.");
        }
        return position;
    }

    private (ActorState Actor, PartyTraceSample Animation, PartyTraceSample Position)[] PreparePartyTraceFollowers(WalkmeshPosition position, int direction, bool moved, PartyTraceState? diagnosticTrace = null)
    {
        PartyTraceState trace = diagnosticTrace ?? Scene.PartyTrace ?? throw new NotSupportedException("Native party trace is unavailable.");
        ActorState player = ResolvePartyActor(0) ?? throw new NotSupportedException("Native party trace has no leader binding.");
        int playerAnimation = moved ? NativePlayerWalking ? player.WalkAnimationId : player.RunAnimationId : player.BaseAnimationId;
        if (!HasNativeEmptyAnimationModel(player) && AnimationFrameCount?.Invoke(player.RenderModelId, playerAnimation) is not > 0)
        {
            throw new NotSupportedException("Native player trace requires complete animation metadata before movement.");
        }
        List<(ActorState, PartyTraceSample, PartyTraceSample)> followers = [];
        int nextCursor = moved ? (trace.Cursor + 1) & 63 : trace.Cursor;
        for (int slot = 1; slot < 3; slot++)
        {
            if (ResolvePartyActor(slot) is not ActorState follower)
            {
                continue;
            }
            if (follower.NativeLadderMode3 is { Active: true })
            {
                throw new NotSupportedException("Native follower trace placement during active mode3 requires its shared physical placement-phase contract.");
            }
            PartyTraceSample?[] trail = slot == 1 ? trace.FirstTrail : trace.SecondTrail;
            int offset = slot == 1 ? trace.FirstOffset : trace.SecondOffset;
            PartyTraceSample animation = trail[(trace.Cursor - offset) & 63] ?? throw new NotSupportedException("Native party animation would observe an opaque trace record; original positions preserved.");
            PartyTraceSample sample = trail[(nextCursor - offset) & 63] ?? throw new NotSupportedException("Native party movement would observe an opaque trace record; original positions preserved.");
            if (!HasNativeEmptyAnimationModel(follower) && (!TryReadNativeBaseAnimations(follower, out int idle, out _, out _) || animation.Animation is < 0 or > 5 || animation.Animation >= 3 && follower.NativeLadderAnimation is null || AnimationFrameCount?.Invoke(follower.RenderModelId, moved ? LadderAnimationFor(follower, animation.Animation) : idle) is not > 0))
            {
                throw new NotSupportedException("Native follower trace requires proved animation mode and model metadata.");
            }
            followers.Add((follower, animation, sample));
        }
        return followers.ToArray();
    }

    private void ApplyPartyTraceMovement(ActorState player, FieldFixedPosition position, int direction, bool moved, PartyTraceState? diagnosticTrace = null)
    {
        PartyTraceState trace = diagnosticTrace ?? Scene.PartyTrace!;
        WalkmeshPosition display = new(new Vector3(position.X / 4096f, position.Y / 4096f, position.Z / 4096f), player.WalkmeshTriangle);
        var followers = PreparePartyTraceFollowers(display, direction, moved, diagnosticTrace);
        player.NativeLadder = null;
        player.NativeLadderMode3 = null;
        if (diagnosticTrace is not null)
        {
            Scene.PartyTrace = diagnosticTrace;
        }
        else
        {
            int animationMode = moved ? NativePlayerWalking ? 0 : 1 : trace.FirstTrail[trace.Cursor]?.Animation ?? -1;
            int recordedDirection = moved ? direction : trace.FirstTrail[trace.Cursor]?.Direction ?? -1;
            PartyTraceSample sample = new((short)(position.X / 4096), (short)(position.Y / 4096), (short)(position.Z / 4096), player.WalkmeshTriangle, recordedDirection, animationMode);
            PartyTraceSample?[] first = (PartyTraceSample?[])trace.FirstTrail.Clone();
            PartyTraceSample?[] second = (PartyTraceSample?[])trace.SecondTrail.Clone();
            first[trace.Cursor] = sample;
            second[trace.Cursor] = moved ? sample : sample with { Animation = trace.SecondTrail[trace.Cursor]?.Animation ?? -1, Direction = trace.SecondTrail[trace.Cursor]?.Direction ?? -1 };
            Scene.PartyTrace = trace with { Cursor = moved ? (trace.Cursor + 1) & 63 : trace.Cursor, FirstTrail = first, SecondTrail = second };
        }
        Scene.NativePlayerPosition = position;
        if (moved && diagnosticTrace is null)
        {
            player.MovementSpeed = NativePlayerMovementSpeed;
        }
        if (player.NativeScriptMovement is { } playerMovement)
        {
            NativeMotionAuthority? authority = player.NativeMotionAuthority;
            byte hold = authority?.FacingHold ?? NativeMoveFacingHold(playerMovement);
            // Ordinary4789A0 placement updates the same physical body retained by a completed MOVE.
            player.NativeScriptMovement = playerMovement with { FixedX = position.X, FixedY = position.Y, FixedZ = position.Z, Triangle = player.WalkmeshTriangle, Speed = checked((short)player.MovementSpeed), MovementDirection = authority?.MovementDirection ?? playerMovement.MovementDirection, FacingDirection = (byte)player.Direction, FacingHold576 = hold == (playerMovement.HoldsFacing ? 1 : 0) ? null : hold };
        }
        int playerAnimation = moved ? NativePlayerWalking ? player.WalkAnimationId : player.RunAnimationId : player.BaseAnimationId;
        if (HasNativeEmptyAnimationModel(player))
        {
            SelectFullNativeAnimation(player, playerAnimation, player.AnimationId != playerAnimation, false);
        }
        else
        {
            StepLadderAnimation(player, playerAnimation, 1, moved);
        }
        foreach (var follower in followers)
        {
            int selected = moved ? LadderAnimationFor(follower.Actor, follower.Animation.Animation) : follower.Actor.BaseAnimationId;
            if (HasNativeEmptyAnimationModel(follower.Actor))
            {
                SelectFullNativeAnimation(follower.Actor, selected, follower.Actor.AnimationId != selected, false);
            }
            else
            {
                StepLadderAnimation(follower.Actor, selected, moved ? unchecked((sbyte)follower.Animation.SecondFlag) : 1);
            }
            NativeMotionAuthority? authority = follower.Actor.NativeMotionAuthority;
            NativeScriptMovementState? movement = follower.Actor.NativeScriptMovement;
            follower.Actor.InvalidateNativeMotionAuthority();
            // Native478430 changes body577/collision588, not575/576. Keep
            // known motion scalars; legacy absent authority stays absent.
            follower.Actor.NativeMotionAuthority = authority;
            follower.Actor.X = follower.Position.X;
            follower.Actor.Y = follower.Position.Y;
            follower.Actor.Z = follower.Position.Z;
            follower.Actor.WalkmeshTriangle = follower.Position.Triangle;
            follower.Actor.NativeMovementGroundContact600 = unchecked((byte)follower.Position.FirstFlag);
            if (moved || diagnosticTrace is not null)
            {
                follower.Actor.Direction = follower.Animation.Direction;
                follower.Actor.CollisionEnabled = false;
            }
            // Trace placement changes XYZ/triangle/577, not mode572, its target,
            // completion542 or the VM373 issued bit (478430/478680).
            if (movement is not null)
            {
                follower.Actor.NativeScriptMovement = movement with { FixedX = follower.Position.X * 4096, FixedY = follower.Position.Y * 4096, FixedZ = follower.Position.Z * 4096, Triangle = follower.Position.Triangle, FacingDirection = (byte)follower.Actor.Direction };
            }
        }
    }

    private void StepStationaryPartyTrace()
    {
        if (Scene.NativePartyTraceVersion != 1 || !IsUserControlEnabled || IsMovementBlockedByDialogue || partyJoin is not null)
        {
            return;
        }
        ActorState player = ResolvePartyActor(0)!;
        if (NativePlayerHasScriptedMode(player))
        {
            return;
        }
        FieldFixedPosition position = ReadNativePartyPlayerPosition(player);
        PreparePartyTraceFollowers(new WalkmeshPosition(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle), (int)player.Direction, false);
        ApplyPartyTraceMovement(player, position, (int)player.Direction, false);
    }
}
