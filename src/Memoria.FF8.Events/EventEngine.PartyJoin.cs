using System.Numerics;

namespace Memoria.FF8.Events;

public sealed record PartyJoinStepQuery(int Triangle, int FixedX, int FixedY, int FixedZ, int Radius, int Direction);
public sealed record NativeJoinMovementQuery(int ObjectId, int Triangle, int FixedX, int FixedY, int FixedZ, int Direction, short Speed, int Radius, Func<NativePlayerGroundProbe, bool>? HasActorCollision = null);
public sealed record NativeJoinMovementProposal(int FixedX, int FixedY, int Direction);
public sealed record NativeJoinMovementResult(bool Accepted, int FixedX, int FixedY, int FixedZ, int Triangle, int Direction, IReadOnlyList<NativeJoinMovementProposal> Proposals, IReadOnlyList<IReadOnlyList<NativePlayerGroundProbe>>? ProposalGroundProbes = null);
public sealed record PartyJoinTarget(int ObjectId, int FixedX, int FixedY, int FixedZ, int TargetX, int TargetY, int TargetZ, int Speed, int Completion);
public sealed record PartyJoinState(long FiberSequence, int CallerObjectId, PartyJoinTarget[] Targets, bool UsesNativeMovement = false);

public sealed partial class EventEngine
{
    private PartyJoinState? partyJoin;
    public Func<int, int, int, int?>? ResolvePartyTraceTriangle { get; set; }
    public Func<PartyJoinStepQuery, WalkmeshPosition?>? ResolvePartyJoinStep { get; set; }
    public Func<NativeJoinMovementQuery, NativeJoinMovementResult?>? ResolveNativeJoinMovement { get; set; }
    public Func<ActorState, bool>? CanUseNativeJoinMovement { get; set; }

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
        if (actor.NativeBaseAnimationVersion == 0)
        {
            if (actor.BaseFirstFrame == 0 && actor.BaseLastFrame == 0 && actor.BaseAnimationId == 0)
            {
                return false;
            }
            // The old handler stored the original two BASEANIME operands here.
            walk = (byte)actor.BaseFirstFrame;
            run = (byte)actor.BaseLastFrame;
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
        if (Scene.NativeActorLayoutVersion != 1 || Scene.FieldPartyNativeActorIds.Count != 3 || ResolvePartyActor(0)?.ObjectId != fiber.ObjectId || Scene.Party.Count == 0 || actors[fiber.ObjectId].CharacterId != Scene.Party[0])
        {
            return "Unsupported JOIN requires the proved compact bindings and original party leader caller";
        }
        if (ResolvePartyTraceTriangle is null || ResolvePartyJoinStep is null)
        {
            return "Unsupported JOIN requires native trace geometry and bounded native movement geometry";
        }
        if (actors[fiber.ObjectId].MovementTarget is not null || actors[fiber.ObjectId].MovementActorId is not null)
        {
            return "Unsupported JOIN requires completed original leader movement before fixing companion targets";
        }
        if (ReadNativeJoinLeaderPosition(actors[fiber.ObjectId]) is null)
        {
            return "Unsupported fractional JOIN leader requires its matching preserved native fixed position";
        }
        bool nativeMovement = ResolveNativeJoinMovement is not null && Enumerable.Range(1, 2).Select(ResolvePartyActor).OfType<ActorState>().All(actor => CanUseNativeJoinMovement?.Invoke(actor) == true);
        foreach (ActorState actor in Enumerable.Range(0, 3).Select(ResolvePartyActor).OfType<ActorState>())
        {
            if (!TryReadNativeBaseAnimations(actor, out int idle, out int walk, out int run) || new[] { idle, walk, run }.Any(animation => AnimationFrameCount?.Invoke(actor.ModelId, animation) is not > 0))
            {
                return "Unsupported JOIN requires original idle/walk/run IDs and complete model animation metadata";
            }
            if (actor.ObjectId != fiber.ObjectId && actor.NativeScriptMovement is { Active: true })
            {
                return "JOIN requires completed native companion MOVE before the certified movement driver starts";
            }
            if ((actor.ObjectId != fiber.ObjectId && (actor.X != MathF.Truncate(actor.X) || actor.Y != MathF.Truncate(actor.Y))) || actor.Z != MathF.Truncate(actor.Z) || actor.PushRadius is < 0 or > 200 || actor.Z != actors[fiber.ObjectId].Z && !nativeMovement)
            {
                return "Unsupported JOIN movement requires the reached flat height and integral companions with native peer-envelope checks";
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
                if (ResolvePartyActor(slot) is not ActorState target)
                {
                    continue;
                }
                RegisterLegacyBaseAnimations(target);
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
            RegisterLegacyBaseAnimations(caller);
            SelectFullNativeAnimation(caller, caller.BaseAnimationId, true);
            bool nativeMovement = targets.Count > 0 && targets.All(target => ResolveNativeJoinMovement is not null && CanUseNativeJoinMovement?.Invoke(actors[target.ObjectId]) == true);
            partyJoin = new PartyJoinState(fiber.Sequence, caller.ObjectId, targets.ToArray(), nativeMovement);
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
        }
        partyJoin = null;
    }

    private static void RegisterLegacyBaseAnimations(ActorState actor)
    {
        if (actor.NativeBaseAnimationVersion != 0)
        {
            return;
        }
        if (!TryReadNativeBaseAnimations(actor, out _, out int walk, out int run))
        {
            throw new NotSupportedException("Legacy native base animation operands are unavailable.");
        }
        actor.WalkAnimationId = walk;
        actor.RunAnimationId = run;
        actor.NativeBaseAnimationVersion = 1;
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

    private bool IsCavernNativeMoveJoin => FieldId == 129 && partyJoin is { UsesNativeMovement: true, Targets.Length: 1 } && partyJoin.Targets[0].ObjectId == 5 && actors[5].NativeMotionAuthority is { Version: 1, FacingHold: 0 };

    private void StepJoinedCompanions(bool postVm = false)
    {
        if (partyJoin is null)
        {
            return;
        }
        if (IsCavernNativeMoveJoin != postVm)
        {
            return;
        }
        PartyJoinTarget[] next = new PartyJoinTarget[partyJoin.Targets.Length];
        WalkmeshPosition?[] positions = new WalkmeshPosition?[next.Length];
        int[] directions = new int[next.Length];
        for (int index = 0; index < next.Length; index++)
        {
            PartyJoinTarget target = partyJoin.Targets[index];
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
            int stop = actor.PushRadius + 96;
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
            if (partyJoin.UsesNativeMovement)
            {
                NativeJoinMovementQuery nativeQuery = new(actor.ObjectId, actor.WalkmeshTriangle, target.FixedX, target.FixedY, target.FixedZ, direction, (short)target.Speed, actor.PushRadius, probe => EvaluateNativePlayerGroundContacts(actor, probe).Collision);
                NativeJoinMovementResult movement = ResolveNativeJoinMovement?.Invoke(nativeQuery) ?? throw FreezeJoin("Unsupported native JOIN movement geometry; preserved raw coordinates and JOIN PC require the certified adapter");
                if (movement.Proposals.Count is < 1 or > 16 || (uint)movement.Direction > 255 || movement.Triangle is < 0 or > short.MaxValue || movement.Proposals.Any(proposal => (uint)proposal.Direction > 255) || movement.Accepted && (movement.FixedX != movement.Proposals[^1].FixedX || movement.FixedY != movement.Proposals[^1].FixedY) || !movement.Accepted && (movement.FixedX != target.FixedX || movement.FixedY != target.FixedY || movement.FixedZ != target.FixedZ) || (movement.FixedZ & 4095) != 0)
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
                        foreach (NativeJoinMovementProposal proposal in movement.Proposals)
                        {
                            EnsureNativeActorEnvelopeClear(actor, proposal.FixedX, proposal.FixedY, target.FixedZ, proposal.Direction, false);
                        }
                    }
                }
                catch (NotSupportedException exception)
                {
                    throw FreezeJoin(exception.Message);
                }
                positions[index] = new WalkmeshPosition(new Vector3(movement.FixedX / 4096f, movement.FixedY / 4096f, movement.FixedZ / 4096f), movement.Triangle);
                directions[index] = movement.Direction;
                next[index] = target with { FixedX = movement.FixedX, FixedY = movement.FixedY, FixedZ = movement.FixedZ, Completion = 1 };
                continue;
            }
            int proposalX = unchecked(target.FixedX + NativeSine(direction) * target.Speed / 256);
            int proposalY = unchecked(target.FixedY - NativeCosine(direction) * target.Speed / 256);
            try
            {
                EnsureNativeActorEnvelopeClear(actor, proposalX, proposalY, target.FixedZ, direction, false);
            }
            catch (NotSupportedException exception)
            {
                throw FreezeJoin(exception.Message);
            }
            PartyJoinStepQuery query = new(actor.WalkmeshTriangle, proposalX, proposalY, target.FixedZ, actor.PushRadius, direction);
            WalkmeshPosition position = ResolvePartyJoinStep?.Invoke(query) ?? throw FreezeJoin("Unsupported native JOIN step needs slope, wall or triangle-traversal evidence; current positions and original JOIN PC preserved");
            if (position.Position.Z != target.FixedZ / 4096f || Math.Abs(position.Position.X - proposalX / 4096f) > 0.001f || Math.Abs(position.Position.Y - proposalY / 4096f) > 0.001f)
            {
                throw FreezeJoin("Native JOIN step resolver changed its fixed proposal or flat height");
            }
            positions[index] = position;
            directions[index] = direction;
            next[index] = target with { FixedX = proposalX, FixedY = proposalY, Completion = 1 };
        }
        for (int index = 0; index < next.Length; index++)
        {
            ActorState actor = actors[next[index].ObjectId];
            bool cavernJoin = IsCavernNativeMoveJoin;
            actor.InvalidateNativeMotionAuthority();
            actor.X = next[index].FixedX / 4096f;
            actor.Y = next[index].FixedY / 4096f;
            actor.Z = next[index].FixedZ / 4096f;
            if (positions[index] is WalkmeshPosition position)
            {
                actor.WalkmeshTriangle = position.Triangle;
                actor.Direction = directions[index];
            }
            if (cavernJoin)
            {
                actor.NativeMotionAuthority = new NativeMotionAuthority(1, (byte)actor.Direction, 0);
            }
        }
        partyJoin = partyJoin with { Targets = next };
    }

    private EventScriptException FreezeJoin(string reason)
    {
        Fiber fiber = fibers.Single(item => item.Sequence == partyJoin!.FiberSequence);
        string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: {reason}.";
        LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, "JOIN", message);
        return new EventScriptException(message);
    }

    private void BuildJoinedPartyTrace()
    {
        PartyTraceSample?[] first = ResetTraceFlags(Scene.PartyTrace?.FirstTrail);
        PartyTraceSample?[] second = ResetTraceFlags(Scene.PartyTrace?.SecondTrail);
        BuildJoinedTrail(first, ResolvePartyActor(1));
        BuildJoinedTrail(second, ResolvePartyActor(2));
        Scene.PartyTrace = new PartyTraceState(0, 15, 30, first, second);
        ActorState leader = ResolvePartyActor(0)!;
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
        ActorState leader = ResolvePartyActor(0)!;
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
        bool nativeMovement = Scene.NativePlayerMovementVersion == 1 || ResolveNativePlayerMovement is not null && CanUseNativePlayerMovement?.Invoke(player) == true;
        if (nativeMovement)
        {
            if (axes != Vector2.Zero)
            {
                return ResolveCertifiedNativePlayerStep(player, current, direction, speed);
            }
            ValidatePendingNativePushContacts();
            WalkmeshPosition stationary = new(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle);
            PreparePartyTraceFollowers(stationary, direction, false);
            return (stationary, current, direction, [], false, null);
        }
        int x = unchecked(current.X + NativeSine(direction) * speed / 256);
        int y = unchecked(current.Y - NativeCosine(direction) * speed / 256);
        if (axes == Vector2.Zero)
        {
            x = current.X;
            y = current.Y;
        }
        ValidatePendingNativePushContacts();
        int[] pushContacts = axes == Vector2.Zero ? [] : EnsureNativeActorEnvelopeClear(player, x, y, current.Z, direction, true);
        PartyJoinStepQuery query = new(player.WalkmeshTriangle, x, y, current.Z, player.PushRadius, direction);
        WalkmeshPosition position = ResolvePartyJoinStep?.Invoke(query) ?? throw new NotSupportedException("Native party player movement needs slope, wall or blocked-envelope steering evidence; original position preserved.");
        if (position.Position.Z != current.Z / 4096f)
        {
            throw new NotSupportedException("Native party player movement left its proved flat height; original position preserved.");
        }
        PreparePartyTraceFollowers(position, direction, x != current.X || y != current.Y);
        return (position, new FieldFixedPosition(x, y, current.Z), direction, pushContacts, false, null);
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

    private int[] EnsureNativeActorEnvelopeClear(ActorState player, int x, int y, int z, int direction, bool nativePlayer)
    {
        HashSet<int> pushContacts = [];
        foreach (int offset in new[] { 32, -32, 0 })
        {
            int probeX = unchecked(x + player.PushRadius * NativeSine(direction + offset));
            int probeY = unchecked(y - player.PushRadius * NativeCosine(direction + offset));
            if (player.ModelId is -1 or 65535)
            {
                continue;
            }
            foreach (int objectId in Scene.NativeActorObjectIds)
            {
                ActorState other = actors[objectId];
                if (other.ObjectId == player.ObjectId || other.ModelId is -1 or 65535 || !other.CollisionEnabled && (!nativePlayer || !other.PushEnabled))
                {
                    continue;
                }
                int heightDifference = (int)MathF.Floor(other.Z) - (z >> 12);
                if (heightDifference is <= -127 or >= 128)
                {
                    continue;
                }
                int dx = (checked((int)(other.X * 4096)) - probeX) >> 12;
                int dy = (checked((int)(other.Y * 4096)) - probeY) >> 12;
                int radius = (player.PushRadius + other.PushRadius) / 2;
                if (radius * radius > unchecked(dx * dx + dy * dy))
                {
                    if (other.CollisionEnabled)
                    {
                        throw new NotSupportedException("Native player actor-contact envelope requires collision/steering continuation evidence; original position preserved.");
                    }
                    if (nativePlayer && other.PushEnabled)
                    {
                        ValidateNativePushDispatch(other);
                        pushContacts.Add(other.ObjectId);
                    }
                }
            }
        }
        return pushContacts.ToArray();
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
        if (Scene.NativePartyTraceVersion != 1 || !IsUserControlEnabled || IsDialogueOpen || services.IsMoviePlaying || partyJoin is not null)
        {
            return;
        }
        ActorState player = ResolvePartyActor(0)!;
        FieldFixedPosition position = ReadNativePartyPlayerPosition(player);
        PreparePartyTraceFollowers(new WalkmeshPosition(new Vector3(player.X, player.Y, player.Z), player.WalkmeshTriangle), (int)player.Direction, false);
        ApplyPartyTraceMovement(player, position, (int)player.Direction, false);
    }
}
