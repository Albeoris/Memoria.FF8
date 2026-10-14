using System.Numerics;

namespace Memoria.FF8.Events;

public sealed record NativeJoinMovementQuery(int ObjectId, int Triangle, int FixedX, int FixedY, int FixedZ, int Direction, short Speed, int Radius, Func<NativePlayerGroundProbe, bool>? HasActorCollision = null);
public sealed record NativeJoinMovementProposal(int FixedX, int FixedY, int Direction);
public sealed record NativeJoinMovementResult(bool Accepted, int FixedX, int FixedY, int FixedZ, int Triangle, int Direction, IReadOnlyList<NativeJoinMovementProposal> Proposals, IReadOnlyList<IReadOnlyList<NativePlayerGroundProbe>>? ProposalGroundProbes = null);
public sealed record PartyJoinTarget(int ObjectId, int FixedX, int FixedY, int FixedZ, int TargetX, int TargetY, int TargetZ, int Speed, int Completion);
public sealed record PartyJoinState(long FiberSequence, int CallerObjectId, PartyJoinTarget[] Targets, bool UsesNativeMovement = false);
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
                return "Saved unsupported native JOIN substep requires replay from its original pre-JOIN checkpoint after a contract upgrade";
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
            // TODO(TD-003):.524F40 preserves
            // signed474; nonzero610 cannot consume an inferred accumulator.
            if (actor.MovementDirectionLimit != 0)
            {
                return "JOIN with nonzero MLIMIT requires retained original native474 before target issue";
            }
            if (actor.ObjectId != fiber.ObjectId && actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 })
            {
                return "Native JOIN requires actual companion movement575 and facing hold576 before issue";
            }
            if (!TryReadNativeBaseAnimations(actor, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(actor.ModelId, animation) is not > 0))
            {
                return "Unsupported JOIN requires original idle/walk/run IDs and complete model animation metadata";
            }
            if (actor.ObjectId != fiber.ObjectId && actor.NativeScriptMovement is { Active: true })
            {
                return "JOIN requires completed native companion MOVE before the certified movement driver starts";
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
                target.NativeScriptMovement = null;
                SelectFullNativeAnimation(target, walk ? target.WalkAnimationId : target.RunAnimationId, true);
                targets.Add(new PartyJoinTarget(target.ObjectId, fixedX, fixedY, fixedZ, callerPosition.X, callerPosition.Y, callerPosition.Z, speed, 0));
            }
            SelectFullNativeAnimation(caller, caller.BaseAnimationId, true);
            partyJoin = new PartyJoinState(fiber.Sequence, caller.ObjectId, targets.ToArray(), true);
            fiber.Position--;
            fiber.WaitReason = WaitReason.PartyJoin;
            return;
        }
        foreach (PartyJoinTarget target in partyJoin.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            if (target.Completion == 2 && actor.AnimationId != actor.BaseAnimationId)
            {
                SelectFullNativeAnimation(actor, actor.BaseAnimationId, true);
            }
        }
        if (partyJoin.Targets.Any(target => target.Completion != 2))
        {
            fiber.Position--;
            fiber.WaitReason = WaitReason.PartyJoin;
            return;
        }
        BuildJoinedPartyTrace();
        foreach (PartyJoinTarget target in partyJoin.Targets)
        {
            ActorState actor = actors[target.ObjectId];
            actor.MovementStopDistance = 0;
            if (partyJoin.UsesNativeMovement && NativeRuntimeProfile is { IsVerified: true })
            {
                actor.NativeJoinedPosition = new NativeJoinedPosition(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, Frame, target.FixedX, target.FixedY, target.FixedZ, actor.WalkmeshTriangle, actor.NativeMotionAuthority!);
            }
        }
        partyJoin = null;
    }

    private void SelectFullNativeAnimation(ActorState actor, int animation, bool reset)
    {
        if (HasNativeEmptyAnimationModel(actor))
        {
            // 532A40 routes native536=-1 through531D40, which retains an
            // empty model record.532890(mode13) does not select a resource.
            return;
        }
        int frames = AnimationFrameCount?.Invoke(actor.ModelId, animation) ?? throw new NotSupportedException("Native animation selection requires model frame metadata.");
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
        return NativeRuntimeProfile is { IsVerified: true } && Scene.NativeActorLayoutVersion == 1 && Scene.NativeActorObjectIds.Contains(actor.ObjectId) && actor.ModelId is -1 or ushort.MaxValue;
    }

    private bool StepNativeJoinedCompanion(ActorState actor)
    {
        int index = partyJoin is null ? -1 : Array.FindIndex(partyJoin.Targets, target => target.ObjectId == actor.ObjectId);
        if (index < 0)
        {
            return false;
        }
        PartyJoinTarget target = partyJoin!.Targets[index];
        if (target.Completion == 2)
        {
            return true;
        }
        if (!partyJoin.UsesNativeMovement || ResolveNativeJoinMovement is null || ResolveNativeScriptDirection is null || CanUseNativeJoinMovement?.Invoke(actor) != true || actor.NativeMotionAuthority is not { Version: 1, FacingHold: <= 1 } authority || actor.MovementDirectionLimit != 0 || (uint)actor.Direction > byte.MaxValue || actor.PushRadius is < 0 or > ushort.MaxValue || actor.MovementSpeed != target.Speed || actor.MovementStopDistance != 96)
        {
            throw FreezeJoin("JOIN requires its certified native mode1 adapters, actual motion authority and zero direction limit; nonzero MLIMIT needs retained native474");
        }
        NativeScriptDirectionResult direction;
        try
        {
            // 524F40 preserves474. With610=0,479970 never reads or writes
            // that accumulator; this query placeholder asserts no physical474.
            direction = PrepareNativeDirectionStep(new NativeScriptDirectionQuery(actor.ObjectId, target.FixedX, target.FixedY, target.TargetX, target.TargetY, (short)target.Speed, 96, (ushort)actor.PushRadius, authority.MovementDirection, (byte)actor.Direction, 0, 0, authority.FacingHold != 0));
        }
        catch (NotSupportedException exception)
        {
            throw FreezeJoin(exception.Message);
        }
        PartyJoinTarget next = target with { FixedX = direction.FixedX, FixedY = direction.FixedY, Completion = 2 };
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
            next = target with { FixedX = movement.FixedX, FixedY = movement.FixedY, FixedZ = movement.FixedZ, Completion = 1 };
            actor.WalkmeshTriangle = movement.Triangle;
            if (authority.FacingHold == 0)
            {
                actor.Direction = movement.Direction;
            }
            authority = authority with { MovementDirection = (byte)movement.Direction };
        }
        actor.InvalidateNativeMotionAuthority();
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
        foreach (ActorState actor in saved.Actors.Where(actor => actor.NativeJoinedPosition is not null || actor.NativeSplitPosition is not null))
        {
            NativeJoinedPosition joined = actor.NativeJoinedPosition ?? actor.NativeSplitPosition!;
            Albeoris.Games.FF8.Jsm.Jsm.Opcode opcode = actor.NativeJoinedPosition is not null ? Albeoris.Games.FF8.Jsm.Jsm.Opcode.JOIN : Albeoris.Games.FF8.Jsm.Jsm.Opcode.SPLIT;
            EventScript? script = (uint)joined.CallerObjectId < program!.Objects.Count ? program.Objects[joined.CallerObjectId].Scripts.FirstOrDefault(script => script.Id == joined.ScriptId) : null;
            if (NativeRuntimeProfile is not { IsVerified: true } || joined.Version != 1 || joined.FieldId != saved.FieldId || joined.Frame is < 1 || joined.Frame > saved.Frame || joined.FiberSequence < 1 || script is null || joined.Operation < script.Start || joined.Operation >= script.End || program.Operations[joined.Operation].Opcode != opcode || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId) || opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.JOIN && actor.ObjectId == joined.CallerObjectId || joined.Authority is not { Version: 1, FacingHold: <= 1 } || actor.NativeMotionAuthority != joined.Authority || actor.NativeScriptMovement is not null || actor.NativeJump is not null || actor.X != joined.FixedX / 4096f || actor.Y != joined.FixedY / 4096f || actor.Z != joined.FixedZ / 4096f || actor.WalkmeshTriangle != joined.Triangle || joined.Triangle is < 0 or > short.MaxValue || (joined.FixedZ & 4095) != 0 || new[] { joined.FixedX, joined.FixedY, joined.FixedZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue))
            {
                throw new InvalidDataException("Saved completed JOIN body lost its original instruction, raw position or physical motion authority.");
            }
        }
    }

    private void BuildJoinedPartyTrace()
    {
        PartyTraceSample?[] first = ResetTraceFlags(Scene.PartyTrace?.FirstTrail);
        PartyTraceSample?[] second = ResetTraceFlags(Scene.PartyTrace?.SecondTrail);
        BuildJoinedTrail(first, ResolveJoinPartyActor(1));
        BuildJoinedTrail(second, ResolveJoinPartyActor(2));
        Scene.PartyTrace = new PartyTraceState(0, 15, 30, first, second);
        ActorState leader = ResolveJoinPartyActor(0)!;
        Scene.NativePlayerPosition = ReadNativeJoinLeaderPosition(leader) ?? throw FreezeJoin("Native JOIN leader lost its preserved raw position");
        Scene.NativePartyTraceVersion = 1;
    }

    private static PartyTraceSample?[] ResetTraceFlags(PartyTraceSample?[]? previous)
    {
        return Enumerable.Range(0, 64).Select(index => previous is { Length: 64 } && previous[index] is PartyTraceSample sample ? sample with { FirstFlag = 1, SecondFlag = 1 } : null).ToArray();
    }

    private void BuildJoinedTrail(PartyTraceSample?[] trail, ActorState? follower)
    {
        if (follower is null)
        {
            // Native FF coordinates are outside the retained array. Both native
            // position and animation consumers guard FF before reading this ring.
            Array.Clear(trail);
            return;
        }
        ActorState leader = ResolveJoinPartyActor(0)!;
        FieldFixedPosition leaderPosition = ReadNativeJoinLeaderPosition(leader) ?? throw FreezeJoin("Native JOIN trace leader lost its preserved raw position");
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
            int triangle = ResolvePartyTraceTriangle?.Invoke(sampleX, sampleY, step < count ? leaderZ : followerZ) ?? throw FreezeJoin("Native JOIN trace triangle lookup has no containing original geometry");
            trail[63 - step] = new PartyTraceSample((short)sampleX, (short)sampleY, (short)sampleZ, triangle, direction, step < count ? 0 : 2);
            if (step < count)
            {
                x = unchecked(x + speed * NativeSine(direction) / -256);
                y = unchecked(y + speed * NativeCosine(direction) / 256);
            }
        }
    }

    private (WalkmeshPosition Position, FieldFixedPosition FixedPosition, int Direction, int[] PushContacts, bool NativeMovement, FieldFixedPosition? TriggerPosition) ResolveNativePartyPlayerStep(ActorState player, Vector2 axes)
    {
        FieldFixedPosition current = ReadNativePartyPlayerPosition(player);
        int direction = axes == Vector2.Zero ? (byte)player.Direction : (byte)(NativePartyDirection(0, 0, (int)(axes.X * 4096), (int)(axes.Y * 4096)) + InputAxisDirection);
        int speed = (Scene.RunEnabled ? 203000 : 69020) * 20 >> 9;
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
        return (stationary, current, direction, [], false, null);
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

    private (ActorState Actor, PartyTraceSample Animation, PartyTraceSample Position)[] PreparePartyTraceFollowers(WalkmeshPosition position, int direction, bool moved)
    {
        PartyTraceState trace = Scene.PartyTrace ?? throw new NotSupportedException("Native party trace is unavailable.");
        ActorState player = ResolvePartyActor(0) ?? throw new NotSupportedException("Native party trace has no leader binding.");
        int playerAnimation = moved ? Scene.RunEnabled ? player.RunAnimationId : player.WalkAnimationId : player.BaseAnimationId;
        if (!HasNativeEmptyAnimationModel(player) && AnimationFrameCount?.Invoke(player.ModelId, playerAnimation) is not > 0)
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
            PartyTraceSample?[] trail = slot == 1 ? trace.FirstTrail : trace.SecondTrail;
            int offset = slot == 1 ? trace.FirstOffset : trace.SecondOffset;
            PartyTraceSample animation = trail[(trace.Cursor - offset) & 63] ?? throw new NotSupportedException("Native party animation would observe an opaque trace record; original positions preserved.");
            PartyTraceSample sample = trail[(nextCursor - offset) & 63] ?? throw new NotSupportedException("Native party movement would observe an opaque trace record; original positions preserved.");
            if (!HasNativeEmptyAnimationModel(follower) && (!TryReadNativeBaseAnimations(follower, out int idle, out int walk, out int run) || AnimationFrameCount?.Invoke(follower.ModelId, moved ? animation.Animation switch { 0 => walk, 1 => run, 2 => idle, _ => -1 } : idle) is not > 0))
            {
                throw new NotSupportedException("Native follower trace requires proved animation mode and model metadata.");
            }
            followers.Add((follower, animation, sample));
        }
        return followers.ToArray();
    }

    private void ApplyPartyTraceMovement(ActorState player, FieldFixedPosition position, int direction, bool moved)
    {
        PartyTraceState trace = Scene.PartyTrace!;
        WalkmeshPosition display = new(new Vector3(position.X / 4096f, position.Y / 4096f, position.Z / 4096f), player.WalkmeshTriangle);
        var followers = PreparePartyTraceFollowers(display, direction, moved);
        int animationMode = moved ? Scene.RunEnabled ? 1 : 0 : trace.FirstTrail[trace.Cursor]?.Animation ?? -1;
        int recordedDirection = moved ? direction : trace.FirstTrail[trace.Cursor]?.Direction ?? -1;
        PartyTraceSample sample = new((short)(position.X / 4096), (short)(position.Y / 4096), (short)(position.Z / 4096), player.WalkmeshTriangle, recordedDirection, animationMode);
        PartyTraceSample?[] first = (PartyTraceSample?[])trace.FirstTrail.Clone();
        PartyTraceSample?[] second = (PartyTraceSample?[])trace.SecondTrail.Clone();
        first[trace.Cursor] = sample;
        second[trace.Cursor] = moved ? sample : sample with { Animation = trace.SecondTrail[trace.Cursor]?.Animation ?? -1, Direction = trace.SecondTrail[trace.Cursor]?.Direction ?? -1 };
        Scene.PartyTrace = trace with { Cursor = moved ? (trace.Cursor + 1) & 63 : trace.Cursor, FirstTrail = first, SecondTrail = second };
        Scene.NativePlayerPosition = position;
        if (moved)
        {
            player.MovementSpeed = (Scene.RunEnabled ? 203000 : 69020) * 20 >> 9;
        }
        int playerAnimation = moved ? Scene.RunEnabled ? player.RunAnimationId : player.WalkAnimationId : player.BaseAnimationId;
        if (player.AnimationId != playerAnimation || !player.AnimationPlaying || !player.AnimationLoop)
        {
            SelectFullNativeAnimation(player, playerAnimation, true);
        }
        foreach (var follower in followers)
        {
            int selected = moved ? follower.Animation.Animation switch { 0 => follower.Actor.WalkAnimationId, 1 => follower.Actor.RunAnimationId, _ => follower.Actor.BaseAnimationId } : follower.Actor.BaseAnimationId;
            if (follower.Actor.AnimationId != selected || !follower.Actor.AnimationPlaying || !follower.Actor.AnimationLoop)
            {
                SelectFullNativeAnimation(follower.Actor, selected, true);
            }
            NativeMotionAuthority? authority = follower.Actor.NativeMotionAuthority;
            follower.Actor.InvalidateNativeMotionAuthority();
            // Native478430 changes body577/collision588, not575/576. Keep
            // known motion scalars; legacy absent authority stays absent.
            follower.Actor.NativeMotionAuthority = authority;
            follower.Actor.X = follower.Position.X;
            follower.Actor.Y = follower.Position.Y;
            follower.Actor.Z = follower.Position.Z;
            follower.Actor.WalkmeshTriangle = follower.Position.Triangle;
            if (moved)
            {
                follower.Actor.Direction = follower.Animation.Direction;
                follower.Actor.CollisionEnabled = false;
            }
        }
    }

    private void StepStationaryPartyTrace()
    {
        if (Scene.PartyTraceMovementPending)
        {
            Scene.PartyTraceMovementPending = false;
            return;
        }
        if (Scene.NativePartyTraceVersion != 1 || !IsUserControlEnabled || IsMovementBlockedByDialogue || services.IsMoviePlaying || partyJoin is not null)
        {
            return;
        }
        ActorState player = ResolvePartyActor(0)!;
        if (player.NativeJump is { Active: true } || player.NativeScriptMovement is { Active: true })
        {
            return;
        }
        FieldFixedPosition position = ReadNativePartyPlayerPosition(player);
        PreparePartyTraceFollowers(new WalkmeshPosition(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle), (int)player.Direction, false);
        ApplyPartyTraceMovement(player, position, (int)player.Direction, false);
    }
}
