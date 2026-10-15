using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

/// <summary>Native mode4 owns DWORD400..456 and shared WORD472/474/508/538/542 until its issuing instruction returns2.</summary>
public sealed record NativeLadderState(int Version, bool Active, long FiberSequence, int ScriptId, int Operation, bool Up, int FixedX, int FixedY, int FixedZ, int Triangle, FieldFixedPosition Entry, FieldFixedPosition Approach, FieldFixedPosition ClimbingEnd, FieldFixedPosition Exit, short Duration);

public sealed partial class EventEngine
{
    private string? UnsupportedNativeLadder(Fiber fiber, bool up)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (actor.NativeLadder is { Active: true } current)
        {
            if (current.FiberSequence != fiber.Sequence || current.ScriptId != fiber.Script.Id || current.Operation != fiber.Position || current.Up != up || !MatchesNativeLadderPlacement(actor, current))
            {
                return "Native mode4 requires its original held instruction and preserved physical placement";
            }
        }
        else if (fiber.Stack.Count < 9 || actor.NativeScriptMovement is { Active: true } || actor.NativeJump is { Active: true } || actor.NativeLadderMode3 is { Active: true } || actor.NativeMovementRequest is not null || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.RotationTarget is not null || ReadNativeScriptPosition(actor) is null)
        {
            return "Native mode4 requires nine original DWORD operands and an idle preserved physical body";
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.NativeMovementCompletion542 is null || actor.NativeTurnAccumulator474 is null || actor.NativeLandingTriangle508 is null || Scene.NativeLadderSpeed490E is null or 0 || Scene.NativePartyTraceVersion != 1 || Scene.PartyTrace is null || ButtonInput?.Held is null)
        {
            return "Native mode4 requires allocated shared physical words, executed nonzero speed490E, dual party traces and mapped held input";
        }
        if (!IsNativePartyPlayerBody(actor) || !IsUserControlEnabled)
        {
            return "Native mode4 nonleader/control-disabled automatic traversal is unsupported; original instruction and operands preserved";
        }
        foreach (ActorState member in Enumerable.Range(0, 3).Select(ResolvePartyActor).OfType<ActorState>())
        {
            if (member.NativeLadderMode3 is { Active: true })
            {
                return "Native mode4 follower placement cannot replace an active mode3 body without its physical overlap contract";
            }
            if (member.NativeLadderAnimation is not { } animation || member.NativePartyFlags is not { AutoAnimation4: true } || member.ObjectId == actor.ObjectId && member.FootstepsEnabled || member.AnimationSpeed is < short.MinValue or > ushort.MaxValue || !float.IsFinite(member.AnimationFrame) || member.AnimationFrame * 16 is < 0 or > ushort.MaxValue || member.AnimationFrame * 16 != MathF.Truncate(member.AnimationFrame * 16) || new[] { (int)animation.Start594, animation.End596 }.Any(id => AnimationFrameCount?.Invoke(member.RenderModelId, id) is not (> 0 and <= 4095)) || new[] { (int)animation.Loop595, member.BaseAnimationId }.Any(id => AnimationFrameCount?.Invoke(member.RenderModelId, id) is not (> 0 and <= 4095)))
            {
                return "Native mode4 requires each present party body's allocated animation bytes, native animation ownership and complete model metadata; enabled ladder footsteps are unsupported";
            }
        }
        try
        {
            ValidateLadderTraceSamples(Scene.PartyTrace);
            if (actor.NativeLadder is not { Active: true })
            {
                int[] operands = fiber.Stack.Take(9).Reverse().ToArray();
                _ = NativeLadderDuration(new FieldFixedPosition(unchecked(operands[0] << 12), unchecked(operands[1] << 12), unchecked(operands[2] << 12)), new FieldFixedPosition(unchecked(operands[3] << 12), unchecked(operands[4] << 12), unchecked(operands[5] << 12)), Scene.NativeLadderSpeed490E.Value);
            }
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativeLadder(Fiber fiber, int triangle, bool up)
    {
        ActorState actor = actors[fiber.ObjectId];
        int[] operands = [];
        if (actor.NativeLadder is not { Active: true })
        {
            FieldFixedPosition raw = ReadNativeScriptPosition(actor)!;
            operands = Pop(fiber, 9);
            FieldFixedPosition Point(int index) => new(unchecked(operands[index] << 12), unchecked(operands[index + 1] << 12), unchecked(operands[index + 2] << 12));
            actor.NativeLadder = new NativeLadderState(1, true, fiber.Sequence, fiber.Script.Id, fiber.Position - 1, up, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, raw, Point(0), Point(3), Point(6), 0);
            actor.NativeMovementCompletion542 = 0;
            actor.NativeLandingTriangle508 = unchecked((ushort)triangle);
            actor.NativeScriptMovement = null;
            actor.NativeJump = null;
            actor.NativeLadderMode3 = null;
            actor.NativeJoinedPosition = null;
            actor.NativeSplitPosition = null;
        }
        else if (actor.NativeMovementCompletion542 == 2)
        {
            actor.NativeLadder = actor.NativeLadder with { Active = false };
            //525B60/525CA0 return2: the successor executes within this callback's remaining VM budget.
            return operands;
        }
        fiber.Position--;
        fiber.WaitReason = WaitReason.NativeLadder;
        return operands;
    }

    private static short NativeLadderDuration(FieldFixedPosition approach, FieldFixedPosition end, byte speed, bool allowZero = false)
    {
        int x = unchecked(end.X - approach.X) / 1024;
        int y = unchecked(end.Y - approach.Y) / 1024;
        int z = unchecked(end.Z - approach.Z) / 1024;
        int sum = unchecked(x * x + y * y + z * z);
        if (speed == 0 || sum < 0)
        {
            throw new NotSupportedException("Native mode4 duration requires nonzero speed and the reached nonoverflowing squared-distance contract");
        }
        int root = (int)Math.Sqrt(sum);
        while ((long)(root + 1) * (root + 1) <= sum)
        {
            root++;
        }
        while ((long)root * root > sum)
        {
            root--;
        }
        int duration = root / speed;
        if (duration < (allowZero ? 0 : 1) || duration > short.MaxValue)
        {
            throw new NotSupportedException("Native ladder duration lies outside the represented signed WORD domain for this movement mode");
        }
        return (short)duration;
    }

    private static bool MatchesNativeLadderPlacement(ActorState actor, NativeLadderState ladder) => actor.X == ladder.FixedX / 4096f && actor.Y == ladder.FixedY / 4096f && actor.Z == ladder.FixedZ / 4096f && actor.WalkmeshTriangle == ladder.Triangle;

    private void StepLadderAnimation(ActorState actor, int animation, int step)
    {
        step = unchecked((sbyte)step);
        int total = checked(AnimationFrameCount!(actor.RenderModelId, animation) * 16);
        ushort frame = actor.AnimationId == animation ? unchecked((ushort)(int)(actor.AnimationFrame * 16)) : (ushort)0;
        SelectFullNativeAnimation(actor, animation, actor.AnimationId != animation, false);
        if (step != 0)
        {
            frame = unchecked((ushort)(frame + unchecked((sbyte)step) * unchecked((ushort)actor.AnimationSpeed)));
            if (step > 0 && frame >= total)
            {
                frame = 0;
            }
            else if (step < 0 && frame > total)
            {
                frame = (ushort)(total - 1);
            }
        }
        actor.AnimationFrame = frame / 16f;
    }

    private static void ValidateLadderTraceSamples(PartyTraceState trace)
    {
        if (trace.Cursor is < 0 or > 63 || trace.FirstOffset is < 0 or > 63 || trace.SecondOffset is < 0 or > 63 || trace.FirstTrail.Length != 64 || trace.SecondTrail.Length != 64)
        {
            throw new NotSupportedException("Native mode4 requires complete bounded dual trace rings");
        }
        foreach (PartyTraceSample? sample in trace.FirstTrail.Concat(trace.SecondTrail))
        {
            if (sample is null || sample.X is < short.MinValue or > short.MaxValue || sample.Y is < short.MinValue or > short.MaxValue || sample.Z is < short.MinValue or > short.MaxValue || sample.Triangle is < short.MinValue or > ushort.MaxValue || sample.Direction is < 0 or > 255 || sample.Animation is < 0 or > 5 || sample.FirstFlag is < 0 or > 255 || sample.SecondFlag is < -128 or > 255)
            {
                throw new NotSupportedException("Native mode4 would observe an opaque or malformed physical trace sample");
            }
        }
    }

    private int LadderAnimationFor(ActorState actor, int kind) => kind switch
    {
        0 => actor.WalkAnimationId,
        1 => actor.RunAnimationId,
        2 => actor.BaseAnimationId,
        3 => actor.NativeLadderAnimation!.Start594,
        4 => actor.NativeLadderAnimation!.Loop595,
        5 => actor.NativeLadderAnimation!.End596,
        _ => throw new NotSupportedException("Native follower trace animation kind is unsupported")
    };

    private void RecordLadderTrace(ActorState actor, NativeLadderState current, int? kind, int step, bool advance, bool facingBefore, bool animateFollowers, int multiplier, int firstTarget, int secondTarget)
    {
        PartyTraceState trace = Scene.PartyTrace!;
        PartyTraceSample?[] first = (PartyTraceSample?[])trace.FirstTrail.Clone();
        PartyTraceSample?[] second = (PartyTraceSample?[])trace.SecondTrail.Clone();
        PartyTraceSample Record(PartyTraceSample previous) => previous with { X = unchecked((short)(current.FixedX / 4096)), Y = unchecked((short)(current.FixedY / 4096)), Z = unchecked((short)(current.FixedZ / 4096)), Triangle = current.Triangle, FirstFlag = 0, SecondFlag = unchecked((byte)(sbyte)step), Animation = kind ?? previous.Animation, Direction = facingBefore ? (byte)actor.Direction : previous.Direction };
        first[trace.Cursor] = Record(first[trace.Cursor]!);
        second[trace.Cursor] = Record(second[trace.Cursor]!);
        int cursor = advance ? (trace.Cursor + 1) & 63 : trace.Cursor;
        int firstOffset = advance ? trace.FirstOffset + Math.Sign(firstTarget - trace.FirstOffset) : trace.FirstOffset;
        int secondOffset = advance ? trace.SecondOffset + Math.Sign(secondTarget - trace.SecondOffset) : trace.SecondOffset;
        foreach (int slot in new[] { 2, 1 })
        {
            if (ResolvePartyActor(slot) is not ActorState follower)
            {
                continue;
            }
            PartyTraceSample?[] trail = slot == 1 ? first : second;
            int offset = slot == 1 ? firstOffset : secondOffset;
            int target = slot == 1 ? firstTarget : secondTarget;
            int index = (cursor - offset) & 63;
            PartyTraceSample sample = trail[index]!;
            NativeMotionAuthority? authority = follower.NativeMotionAuthority;
            follower.InvalidateNativeMotionAuthority();
            //478680 owns follower placement now; prior JOIN/SPLIT completion receipts no longer describe DWORD400..408.
            follower.NativeMotionAuthority = authority;
            follower.X = sample.X;
            follower.Y = sample.Y;
            follower.Z = sample.Z;
            follower.WalkmeshTriangle = sample.Triangle;
            follower.NativeMovementGroundContact600 = unchecked((byte)sample.FirstFlag);
            if (animateFollowers)
            {
                if (offset < target)
                {
                    sample = sample with { Animation = 2 };
                    trail[index] = sample;
                }
                follower.Direction = sample.Direction;
                follower.CollisionEnabled = false;
                StepLadderAnimation(follower, LadderAnimationFor(follower, sample.Animation), multiplier * unchecked((sbyte)sample.SecondFlag));
            }
        }
        if (!facingBefore && animateFollowers)
        {
            first[cursor] = first[cursor]! with { Direction = (byte)actor.Direction };
            second[cursor] = second[cursor]! with { Direction = (byte)actor.Direction };
        }
        Scene.PartyTrace = trace with { Cursor = cursor, FirstOffset = firstOffset, SecondOffset = secondOffset, FirstTrail = first, SecondTrail = second };
    }

    private void StepNativeLadder()
    {
        foreach (ActorState actor in actors.Where(body => body.NativeLadder is { Active: true }))
        {
            NativeLadderState current = actor.NativeLadder!;
            Fiber owner = fibers.Single(fiber => fiber.Sequence == current.FiberSequence);
            string? unsupported = UnsupportedNativeLadder(owner, current.Up);
            if (unsupported is not null)
            {
                string opcode = program!.Operations[current.Operation].Opcode.ToString();
                LastFault = new EventFault(FieldId, actor.ObjectId, current.ScriptId, current.Operation, opcode, unsupported);
                throw new EventScriptException(unsupported);
            }
            int stage = actor.NativeMovementCompletion542!.Value;
            ClearNativeDoorCurrentContacts();
            if (stage == 2)
            {
                continue;
            }
            actor.NativeMovementGroundContact600 = 0;
            if (stage == 0)
            {
                current = current with { Entry = new FieldFixedPosition(current.FixedX, current.FixedY, current.FixedZ) };
                actor.NativeTurnAccumulator474 = 0;
                stage = 3;
                actor.NativeMovementCompletion542 = 3;
            }
            short elapsed = actor.NativeTurnAccumulator474!.Value;
            int kind;
            int step;
            int direction = 1;
            FieldFixedPosition start;
            FieldFixedPosition target;
            if (stage == 1)
            {
                ushort held = ButtonInput!.Held!.Value;
                bool reverse = (held & (current.Up ? 0xC000 : 0x3000)) != 0;
                bool forward = !reverse && (held & (current.Up ? 0x3000 : 0xC000)) != 0;
                step = reverse || forward ? ((held & (current.Up ? 0xC000 : 0x3000)) != 0 ? current.Up ? -1 : 1 : current.Up ? 1 : -1) : 0;
                StepLadderAnimation(actor, actor.NativeLadderAnimation!.Loop595, step);
                RecordLadderTrace(actor, current, step == 0 ? null : 4, step, step != 0, step != 0, step != 0, 1, 31, 62);
                if (step == 0)
                {
                    actor.NativeLadder = current;
                    continue;
                }
                direction = reverse ? -1 : 1;
                kind = 4;
                start = current.Approach;
                target = current.ClimbingEnd;
            }
            else if (stage is 3 or 4 or 5)
            {
                kind = stage == 4 ? current.Up ? 5 : 3 : current.Up ? 3 : 5;
                step = stage == 5 ? current.Up ? -1 : 1 : current.Up ? 1 : -1;
                StepLadderAnimation(actor, LadderAnimationFor(actor, kind), step);
                current = current with { Duration = unchecked((short)(AnimationFrameCount!(actor.RenderModelId, actor.AnimationId) - 2)) };
                RecordLadderTrace(actor, current, kind, step, true, false, true, 1, 31, 62);
                start = stage == 3 ? current.Entry : stage == 4 ? current.ClimbingEnd : current.Approach;
                target = stage == 3 ? current.Approach : stage == 4 ? current.Exit : current.Entry;
            }
            else if (stage == 6)
            {
                StepLadderAnimation(actor, actor.BaseAnimationId, -1);
                RecordLadderTrace(actor, current, 2, -1, true, false, true, 2, 15, 30);
                if (elapsed == 0)
                {
                    current = current with { Duration = ResolvePartyActor(2) is not null ? (short)32 : ResolvePartyActor(1) is not null ? (short)16 : (short)1 };
                }
                actor.NativeTurnAccumulator474 = unchecked((short)(elapsed + 1));
                actor.NativeMovementGroundContact600 = 1;
                if (actor.NativeTurnAccumulator474 >= current.Duration)
                {
                    actor.NativeMovementCompletion542 = 2;
                }
                actor.NativeLadder = current;
                continue;
            }
            else
            {
                throw new NotSupportedException($"Native mode4 physical stage{stage} is unsupported");
            }
            current = current with { FixedX = NativeJumpMath.Interpolate(start.X, target.X, current.Duration, elapsed), FixedY = NativeJumpMath.Interpolate(start.Y, target.Y, current.Duration, elapsed), FixedZ = NativeJumpMath.Interpolate(start.Z, target.Z, current.Duration, elapsed) };
            short nextElapsed = unchecked((short)(elapsed + direction));
            actor.NativeTurnAccumulator474 = nextElapsed;
            if (stage == 1 ? direction > 0 ? nextElapsed == current.Duration : nextElapsed < 0 : nextElapsed >= current.Duration)
            {
                actor.NativeTurnAccumulator474 = 0;
                if (stage == 3)
                {
                    current = current with { Duration = NativeLadderDuration(current.Approach, current.ClimbingEnd, Scene.NativeLadderSpeed490E!.Value) };
                    actor.NativeMovementCompletion542 = 1;
                }
                else if (stage == 1)
                {
                    actor.NativeMovementCompletion542 = direction > 0 ? (ushort)4 : (ushort)5;
                }
                else
                {
                    if (stage == 4)
                    {
                        current = current with { Triangle = unchecked((short)actor.NativeLandingTriangle508!.Value) };
                    }
                    actor.NativeMovementCompletion542 = 6;
                }
            }
            actor.NativeLadder = current;
            actor.X = current.FixedX / 4096f;
            actor.Y = current.FixedY / 4096f;
            actor.Z = current.FixedZ / 4096f;
            actor.WalkmeshTriangle = current.Triangle;
            Scene.NativePlayerPosition = new FieldFixedPosition(current.FixedX, current.FixedY, current.FixedZ);
        }
    }

    private void ValidateNativeLadders(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        if (NativeRuntimeProfile is { IsVerified: true } && saved.Scene.NativeLadderSpeed490E is null)
        {
            throw new InvalidDataException("Saved field lacks executed ladder speed490E; recreate its authentic journal");
        }
        foreach (ActorState actor in saved.Actors.Where(body => body.NativeLadder is not null))
        {
            NativeLadderState ladder = actor.NativeLadder!;
            Fiber? owner = restored.SingleOrDefault(fiber => fiber.Sequence == ladder.FiberSequence);
            EventScript? script = program!.Objects[actor.ObjectId].Scripts.SingleOrDefault(item => item.Id == ladder.ScriptId);
            if (ladder.Version != 1 || ladder.Entry is null || ladder.Approach is null || ladder.ClimbingEnd is null || ladder.Exit is null || script is null || ladder.Operation < script.Start || ladder.Operation >= script.End || program.Operations[ladder.Operation].Opcode != (ladder.Up ? Jsm.Opcode.LADDERUP2 : Jsm.Opcode.LADDERDOWN2) || actor.NativeMovementCompletion542 is null or > 6 || actor.NativeMovementCompletion542 == 0 || actor.NativeTurnAccumulator474 is null or < 0 || ladder.Duration < -1 || actor.NativeTurnAccumulator474 > Math.Max(0, (int)ladder.Duration) || ladder.Duration <= 0 && (actor.NativeMovementCompletion542 != 6 || actor.NativeTurnAccumulator474 != 0) || !MatchesNativeLadderPlacement(actor, ladder) || actor.NativeScriptMovement is { Active: true } || actor.NativeJump is { Active: true } || actor.NativeMovementRequest is not null || ladder.Active && (owner is null || owner.Complete || owner.ObjectId != actor.ObjectId || owner.Script.Id != ladder.ScriptId || owner.Position != ladder.Operation || owner.WaitReason != WaitReason.NativeLadder) || !ladder.Active && actor.NativeMovementCompletion542 != 2)
            {
                throw new InvalidDataException("Saved native mode4 lost its physical phase, raw placement or original held instruction");
            }
            if (ladder.Active)
            {
                if (!NativeFieldPartyBinding.TryGetObjectId(saved.Scene, 0, out int leader) || leader != actor.ObjectId || !saved.UserControlEnabled || saved.Scene.PartyTrace is null || saved.Scene.NativePlayerPosition != new FieldFixedPosition(ladder.FixedX, ladder.FixedY, ladder.FixedZ) || saved.Scene.NativeLadderSpeed490E is null or 0 || new[] { ladder.Approach.X, ladder.Approach.Y, ladder.Approach.Z, ladder.ClimbingEnd.X, ladder.ClimbingEnd.Y, ladder.ClimbingEnd.Z, ladder.Exit.X, ladder.Exit.Y, ladder.Exit.Z }.Any(value => (value & 4095) != 0))
                {
                    throw new InvalidDataException("Saved native mode4 lacks its controlled leader and physical trace owner");
                }
                for (int slot = 0; slot < 3; slot++)
                {
                    ActorState? member = NativeFieldPartyBinding.ResolveActor(saved.Scene, saved.Actors, slot);
                    if (member is null)
                    {
                        continue;
                    }
                    if (member.NativeLadderMode3 is { Active: true })
                    {
                        throw new InvalidDataException("Saved native mode4 cannot place a party body owned by active mode3.");
                    }
                    if (member.NativeLadderAnimation is not { } animation || member.NativePartyFlags is not { AutoAnimation4: true } || slot == 0 && member.FootstepsEnabled || !float.IsFinite(member.AnimationFrame) || member.AnimationFrame * 16 is < 0 or > ushort.MaxValue || member.AnimationFrame * 16 != MathF.Truncate(member.AnimationFrame * 16) || member.AnimationSpeed is < short.MinValue or > ushort.MaxValue || new[] { (int)animation.Start594, animation.End596 }.Any(id => AnimationFrameCount?.Invoke(member.RenderModelId, id) is not (> 0 and <= 4095)) || new[] { (int)animation.Loop595, member.BaseAnimationId }.Any(id => AnimationFrameCount?.Invoke(member.RenderModelId, id) is not (> 0 and <= 4095)))
                    {
                        throw new InvalidDataException("Saved native mode4 has inconsistent physical animation ownership or metadata");
                    }
                }
                int stage = actor.NativeMovementCompletion542!.Value;
                int counter = actor.NativeTurnAccumulator474!.Value;
                int kind = stage == 4 ? ladder.Up ? 5 : 3 : ladder.Up ? 3 : 5;
                if (stage == 6 && counter == 0 && (actor.AnimationId != actor.NativeLadderAnimation!.Start594 && actor.AnimationId != actor.NativeLadderAnimation.End596 || ladder.Duration != AnimationFrameCount!(actor.RenderModelId, actor.AnimationId) - 2))
                {
                    throw new InvalidDataException("Saved native mode4 drain entry lost its selected transition clip and signed duration");
                }
                if ((stage == 3 || stage is 4 or 5 && counter > 0) && ladder.Duration != AnimationFrameCount!(actor.RenderModelId, LadderAnimationFor(actor, kind)) - 2 || (stage == 2 || stage == 6 && counter > 0) && ladder.Duration != (NativeFieldPartyBinding.ResolveActor(saved.Scene, saved.Actors, 2) is not null ? 32 : NativeFieldPartyBinding.ResolveActor(saved.Scene, saved.Actors, 1) is not null ? 16 : 1))
                {
                    throw new InvalidDataException("Saved native mode4 duration disagrees with its selected phase animation or follower drain");
                }
                try
                {
                    ValidateLadderTraceSamples(saved.Scene.PartyTrace);
                }
                catch (NotSupportedException exception)
                {
                    throw new InvalidDataException(exception.Message, exception);
                }
            }
        }
        if (restored.Any(fiber => fiber.WaitReason == WaitReason.NativeLadder && saved.Actors[fiber.ObjectId].NativeLadder is not { Active: true }))
        {
            throw new InvalidDataException("Saved native ladder wait has no preserved mode4 owner");
        }
    }
}
