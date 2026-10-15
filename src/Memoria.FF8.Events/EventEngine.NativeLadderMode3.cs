using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Native mode572=3 owns its entry/target DWORDs, WORD472/474/508/538/542 and current renderer frame.</summary>
public sealed record NativeLadderMode3State([property: JsonRequired] int Version, [property: JsonRequired] bool Active, [property: JsonRequired] long FiberSequence, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] bool Up, [property: JsonRequired] int FixedX, [property: JsonRequired] int FixedY, [property: JsonRequired] int FixedZ, [property: JsonRequired] int Triangle, [property: JsonRequired] FieldFixedPosition Entry, [property: JsonRequired] FieldFixedPosition Target, [property: JsonRequired] short Duration, [property: JsonRequired] byte InitializationSpeed);

public sealed partial class EventEngine
{
    private bool IsControlledLadderMode3(ActorState actor) => IsNativePartyPlayerBody(actor) && IsUserControlEnabled;

    private string? UnsupportedNativeLadderMode3(Fiber fiber, bool up)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativeActorLayoutVersion != 1 || !Scene.NativeActorObjectIds.Contains(actor.ObjectId) || actor.NativeMovementCompletion542 is null || actor.NativeTurnAccumulator474 is null || actor.NativeLandingTriangle508 is null || Scene.NativeLadderSpeed490E is null)
        {
            return "Native mode3 requires its retained physical words and executed ladder speed490E owner";
        }
        if (Scene.NativeLadderSpeed490E == 0 && (actor.NativeLadderMode3 is not { Active: true } || actor.NativeMovementCompletion542 == 0))
        {
            return "Native mode3 initialization requires a nonzero speed490E divisor";
        }
        if (actor.NativeLadderMode3 is { Active: true } current)
        {
            if (current.FiberSequence != fiber.Sequence || current.ScriptId != fiber.Script.Id || current.Operation != fiber.Position || current.Up != up || !MatchesNativeLadderMode3Placement(actor, current))
            {
                return "Native mode3 requires its original held instruction and preserved raw placement";
            }
        }
        else if (fiber.Stack.Count < 4 || actor.NativeScriptMovement is { Active: true } || actor.NativeJump is { Active: true } || actor.NativeLadder is { Active: true } || actor.NativeMovementRequest is not null || partyJoin?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true || partySplit?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true || actor.MovementTarget is not null || actor.MovementActorId is not null || actor.RotationTarget is not null || ReadNativeScriptPosition(actor) is null)
        {
            return "Native mode3 requires four original operands and an idle retained physical body; overlapping movement backups are unsupported";
        }
        if (IsControlledLadderMode3(actor) && (ButtonInput?.Held is null || actor.FootstepsEnabled))
        {
            return "Controlled native mode3 requires mapped held input and the proved disabled-footsteps animation path";
        }
        try
        {
            NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
            if (header.BoundModelId is null or < 0 || header.Frame is null || header.Terminal is null || header.Flags is null || !float.IsFinite(actor.AnimationFrame) || actor.AnimationFrame * 16 is < 0 or > ushort.MaxValue || actor.AnimationFrame * 16 != MathF.Truncate(actor.AnimationFrame * 16))
            {
                return "Native mode3 requires actual bound-resource animation header and renderer WORD82 ownership";
            }
            if (actor.NativeLadderMode3 is not { Active: true })
            {
                NativeScalarValue[] operands = fiber.Stack.Capture().Take(4).Reverse().ToArray();
                if (operands.Take(3).Any(value => !value.IsExact) || !operands[3].ToWidth(2).IsExact)
                {
                    return "Native mode3 requires exact DWORD coordinates and a known animation selector WORD";
                }
                int animation = unchecked((short)operands[3].ToWidth(2).RequireExact());
                if (animation is < 0 or > 255 || AnimationFrameCount?.Invoke(header.BoundModelId.Value, animation) is not (> 0 and <= 4095))
                {
                    return "Native mode3 animation selector or model WORD12 lies outside the proved BYTE animation domain";
                }
                if (actor.NativeScriptMovement is { } previous && (previous.Asynchronous && previous.Cancelled || previous.AnimationFlag40000 == true) || actor.NativeJump is { } jump && (jump.AsyncFlag20000 != false || jump.AnimationFlag40000 != false))
                {
                    return "Native mode3 cannot discard an unconsumed asynchronous movement flag owner";
                }
                FieldFixedPosition target = new(unchecked(operands[0].RequireExact() << 12), unchecked(operands[1].RequireExact() << 12), unchecked(operands[2].RequireExact() << 12));
                _ = NativeLadderDuration(ReadNativeScriptPosition(actor)!, target, Scene.NativeLadderSpeed490E.Value, true);
            }
            else if (AnimationFrameCount?.Invoke(header.BoundModelId.Value, actor.AnimationId) is not (> 0 and <= 4095))
            {
                return "Native mode3 lost its actual current renderer animation resource";
            }
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativeLadderMode3(Fiber fiber, int triangle, bool up, out NativeScalarValue[]? scalarArguments)
    {
        ActorState actor = actors[fiber.ObjectId];
        int[] args = [];
        scalarArguments = null;
        if (actor.NativeLadderMode3 is not { Active: true })
        {
            NativeScalarValue[] original = fiber.Stack.Capture().Take(4).Reverse().ToArray();
            args = original.Select((value, index) => index == 3 && !value.IsExact ? value.ToWidth(2).RequireExact() : value.RequireExact()).ToArray();
            scalarArguments = original.Any(value => !value.IsExact) ? original : null;
            fiber.LastExpressions = fiber.StackExpressions.Take(4).Reverse().ToArray();
            FieldFixedPosition raw = ReadNativeScriptPosition(actor)!;
            for (int index = 0; index < 4; index++)
            {
                PopNativeScalar(fiber);
            }
            actor.NativeScriptMovement = null;
            actor.NativeJump = null;
            actor.NativeLadder = null;
            actor.NativeJoinedPosition = null;
            actor.NativeSplitPosition = null;
            actor.NativeLadderMode3 = new NativeLadderMode3State(1, true, fiber.Sequence, fiber.Script.Id, fiber.Position - 1, up, raw.X, raw.Y, raw.Z, actor.WalkmeshTriangle, raw, new FieldFixedPosition(unchecked(args[0] << 12), unchecked(args[1] << 12), unchecked(args[2] << 12)), 0, Scene.NativeLadderSpeed490E!.Value);
            actor.NativeLandingTriangle508 = unchecked((ushort)triangle);
            actor.NativeMovementCompletion542 = 0;
            SelectNativeBodyAnimation(actor, unchecked((byte)args[3]), AnimationRepeating);
        }
        else if (actor.NativeMovementCompletion542 == 2)
        {
            actor.NativeLadderMode3 = actor.NativeLadderMode3 with { Active = false };
            //525900/525A30 return2 without resetting the loop animation or yielding the actor's remaining budget.
            return args;
        }
        fiber.Position--;
        fiber.WaitReason = WaitReason.NativeLadderMode3;
        return args;
    }

    private static bool MatchesNativeLadderMode3Placement(ActorState actor, NativeLadderMode3State current) => actor.X == current.FixedX / 4096f && actor.Y == current.FixedY / 4096f && actor.Z == current.FixedZ / 4096f && actor.WalkmeshTriangle == current.Triangle;

    private void StepNativeLadderMode3()
    {
        foreach (ActorState actor in actors.Where(body => body.NativeLadderMode3 is { Active: true }))
        {
            NativeLadderMode3State current = actor.NativeLadderMode3!;
            Fiber owner = fibers.Single(fiber => fiber.Sequence == current.FiberSequence);
            string? unsupported = UnsupportedNativeLadderMode3(owner, current.Up);
            if (unsupported is not null)
            {
                LastFault = new EventFault(FieldId, actor.ObjectId, current.ScriptId, current.Operation, program!.Operations[current.Operation].Opcode.ToString(), unsupported);
                throw new EventScriptException(unsupported);
            }
            if (actor.NativeMovementCompletion542 == 0)
            {
                FieldFixedPosition entry = new(current.FixedX, current.FixedY, current.FixedZ);
                actor.NativeLadderMode3 = current with { Entry = entry, Duration = NativeLadderDuration(entry, current.Target, Scene.NativeLadderSpeed490E!.Value, true), InitializationSpeed = Scene.NativeLadderSpeed490E.Value };
                actor.NativeTurnAccumulator474 = 0;
                actor.NativeMovementCompletion542 = 1;
                if (IsNativePartyPlayerBody(actor))
                {
                    Scene.NativePlayerPosition = entry;
                    ClearNativeDoorCurrentContacts();
                }
                continue;
            }
            short elapsed = actor.NativeTurnAccumulator474!.Value;
            if (!IsControlledLadderMode3(actor))
            {
                if (elapsed == current.Duration)
                {
                    current = current with { Triangle = unchecked((short)actor.NativeLandingTriangle508!.Value) };
                    actor.NativeMovementCompletion542 = 2;
                }
                else
                {
                    elapsed = unchecked((short)(elapsed + 1));
                    ushort frame = unchecked((ushort)((int)(actor.AnimationFrame * 16) + RequireNativeAnimationHeader(actor).Increment));
                    int terminal = NativeAnimationTerminal(actor, actor.AnimationId);
                    actor.AnimationFrame = (frame > terminal - 1 ? 0 : frame) / 16f;
                }
            }
            else
            {
                ushort held = ButtonInput!.Held!.Value;
                if ((held & (current.Up ? 0xC000 : 0x3000)) != 0)
                {
                    if (elapsed == 0)
                    {
                        actor.NativeMovementCompletion542 = 2;
                    }
                    else
                    {
                        elapsed--;
                        StepLadderAnimation(actor, actor.AnimationId, -1);
                    }
                }
                // Native mode3 tests forward independently after reverse, unlike mode4's priority branch.
                if ((held & (current.Up ? 0x1000 : 0x4000)) != 0)
                {
                    if (elapsed == current.Duration)
                    {
                        current = current with { Triangle = unchecked((short)actor.NativeLandingTriangle508!.Value) };
                        actor.NativeMovementCompletion542 = 2;
                    }
                    else
                    {
                        elapsed++;
                        StepLadderAnimation(actor, actor.AnimationId, 1);
                    }
                }
            }
            actor.NativeTurnAccumulator474 = elapsed;
            current = current with { FixedX = NativeJumpMath.Interpolate(current.Entry.X, current.Target.X, current.Duration, elapsed), FixedY = NativeJumpMath.Interpolate(current.Entry.Y, current.Target.Y, current.Duration, elapsed), FixedZ = NativeJumpMath.Interpolate(current.Entry.Z, current.Target.Z, current.Duration, elapsed) };
            actor.NativeLadderMode3 = current;
            actor.X = current.FixedX / 4096f;
            actor.Y = current.FixedY / 4096f;
            actor.Z = current.FixedZ / 4096f;
            actor.WalkmeshTriangle = current.Triangle;
            if (IsNativePartyPlayerBody(actor))
            {
                Scene.NativePlayerPosition = new FieldFixedPosition(current.FixedX, current.FixedY, current.FixedZ);
            }
        }
    }

    private void ValidateNativeLaddersMode3(Checkpoint saved, IReadOnlyList<Fiber> restored)
    {
        foreach (ActorState actor in saved.Actors.Where(body => body.NativeLadderMode3 is not null))
        {
            NativeLadderMode3State current = actor.NativeLadderMode3!;
            EventScript? script = program!.Objects[actor.ObjectId].Scripts.SingleOrDefault(item => item.Id == current.ScriptId);
            Fiber? owner = restored.SingleOrDefault(fiber => fiber.Sequence == current.FiberSequence);
            int? counter = actor.NativeTurnAccumulator474;
            if (NativeRuntimeProfile is not { IsVerified: true } || saved.Scene.NativeActorLayoutVersion != 1 || !saved.Scene.NativeActorObjectIds.Contains(actor.ObjectId))
            {
                throw new InvalidDataException("Saved native mode3 requires its verified retained physical actor allocation.");
            }
            if (current.Version != 1 || current.Entry is null || current.Target is null || current.Duration < 0 || script is null || current.Operation < script.Start || current.Operation >= script.End || program.Operations[current.Operation].Opcode != (current.Up ? Jsm.Opcode.LADDERUP : Jsm.Opcode.LADDERDOWN) || actor.NativeMovementCompletion542 is not (1 or 2) || counter is null or < 0 || counter > current.Duration || !MatchesNativeLadderMode3Placement(actor, current) || actor.NativeScriptMovement is not null || actor.NativeJump is not null || actor.NativeLadder is not null || actor.NativeMovementRequest is not null || saved.Scene.NativeLadderSpeed490E is null || new[] { current.Target.X, current.Target.Y, current.Target.Z }.Any(value => (value & 4095) != 0) || current.Active && (owner is null || owner.Complete || owner.ObjectId != actor.ObjectId || owner.Script.Id != current.ScriptId || owner.Position != current.Operation || owner.WaitReason != WaitReason.NativeLadderMode3) || !current.Active && actor.NativeMovementCompletion542 != 2)
            {
                throw new InvalidDataException("Saved native mode3 lost its raw body, phase or original held instruction.");
            }
            if (current.Active && actor.NativeLandingTriangle508 != unchecked((ushort)program.Operations[current.Operation].Parameter))
            {
                throw new InvalidDataException("Saved active mode3 landing WORD508 differs from its issuing instruction.");
            }
            if (current.Active && (saved.PartyJoin?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true || saved.PartySplit?.Targets.Any(target => target.ObjectId == actor.ObjectId) == true))
            {
                throw new InvalidDataException("Saved active mode3 cannot share its physical body with pending JOIN or SPLIT targets.");
            }
            if (NativeFieldPartyBinding.TryGetObjectId(saved.Scene, 0, out int player) && player == actor.ObjectId && saved.Scene.NativePlayerPosition != new FieldFixedPosition(current.FixedX, current.FixedY, current.FixedZ))
            {
                throw new InvalidDataException("Saved mode3 player position mirror differs from its actual physical body.");
            }
            if (current.FixedX != NativeJumpMath.Interpolate(current.Entry.X, current.Target.X, current.Duration, (short)counter.Value) || current.FixedY != NativeJumpMath.Interpolate(current.Entry.Y, current.Target.Y, current.Duration, (short)counter.Value) || current.FixedZ != NativeJumpMath.Interpolate(current.Entry.Z, current.Target.Z, current.Duration, (short)counter.Value))
            {
                throw new InvalidDataException("Saved native mode3 position disagrees with its entry, target and actual counter.");
            }
            try
            {
                if (current.Duration != NativeLadderDuration(current.Entry, current.Target, current.InitializationSpeed, true))
                {
                    throw new InvalidDataException("Saved native mode3 duration disagrees with its initialized entry, target and speed490E.");
                }
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidDataException(exception.Message, exception);
            }
        }
        if (restored.Any(fiber => fiber.WaitReason == WaitReason.NativeLadderMode3 && saved.Actors[fiber.ObjectId].NativeLadderMode3 is not { Active: true }))
        {
            throw new InvalidDataException("Saved native mode3 wait lacks its physical owner.");
        }
    }
}
