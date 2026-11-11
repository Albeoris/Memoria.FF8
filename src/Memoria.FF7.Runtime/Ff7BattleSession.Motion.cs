using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattlePosition(int X, int Y, int Z)
{
    public Vector3 ToVector() => new(X, Y, Z);
    public static Ff7BattlePosition FromVector(Vector3 position) => new((int)position.X, (int)position.Y, (int)position.Z);
    public static Ff7BattlePosition operator +(Ff7BattlePosition a, Ff7BattlePosition b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Ff7BattlePosition operator -(Ff7BattlePosition a, Ff7BattlePosition b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Ff7BattlePosition operator /(Ff7BattlePosition value, int divisor) => new(value.X / divisor, value.Y / divisor, value.Z / divisor);
}

public sealed record Ff7BattleMotion(Ff7BattlePosition Position, Ff7BattlePosition Step, int Remaining, short[]? VerticalSteps = null)
{
    public Ff7BattleMotion Advance()
    {
        if (Remaining == 0)
        {
            return this;
        }
        Ff7BattlePosition delta = VerticalSteps is { } heights ? Step with { Y = heights[heights.Length - Remaining] } : Step;
        return this with { Position = Position + delta, Remaining = Remaining - 1 };
    }
}

public sealed partial class Ff7BattleSession
{
    public Ff7BattlePlacement VisualPlacement(int slot)
    {
        Ff7BattlePlacement home = HomePlacement(slot);
        Ff7BattleActorModelState? model = state.ActorModels.SingleOrDefault(model => model.Slot == slot);
        Vector3 position = model?.Position?.ToVector() ?? home.Position;
        int yaw = model?.FacingYaw ?? home.Yaw;
        if (state.Action is { } action && action.Command.ActorSlot == slot)
        {
            return home with { Position = action.Motion?.Position.ToVector() ?? position, Yaw = action.FacingYaw ?? yaw };
        }
        if (slot >= 3 || !IsAlive(Actor(slot)))
        {
            return home with { Position = position, Yaw = yaw };
        }
        if (Outcome == Ff7BattleOutcome.Escaped)
        {
            int runYaw = Layout == 4 ? state.Actors.Any(actor => actor.Actor.Slot >= 4 && IsAlive(actor) && FacingGroup(actor.Actor) == 0) ? 0 : 2048 : home.Yaw;
            int direction = runYaw == 0 ? 1 : -1;
            return home with { Position = home.Position + new Vector3(0, 0, direction * 400 * state.TerminalFrames), Yaw = runYaw };
        }
        if (Outcome == Ff7BattleOutcome.Victory && CelebrateVictory && Layout is 2 or 4)
        {
            return home with { Yaw = Layout == 2 ? 2048 : 0 };
        }
        return home with { Position = position, Yaw = yaw };
    }

    public (int Clip, int Frame) VisualPose(int slot)
    {
        Ff7BattleActorState actor = Actor(slot);
        Ff7BattleActorModelState? modelState = state.ActorModels.SingleOrDefault(model => model.Slot == slot);
        if (modelState?.Pose is { ClipIndex: >= 0 } modelPose)
        {
            return (modelPose.ClipIndex, modelPose.ClipFrame);
        }
        if (modelState?.IdleStarted is null && state.Action is { Pose: { ClipIndex: >= 0 } pose } action && action.Command.ActorSlot == slot)
        {
            return (pose.ClipIndex, pose.ClipFrame);
        }
        string prefix = Ff7BattleAnimationResources.ModelPrefix(actor.Identity, slot >= 4);
        if (slot < 3 && (actor.Actor.Statuses & 1) != 0)
        {
            const int knockedOutClip = 6;
            return (knockedOutClip, 0);
        }
        if (slot < 3 && Outcome == Ff7BattleOutcome.Victory && CelebrateVictory)
        {
            const int victoryClip = 2;
            int count = animations!.ClipFrameCount(prefix, victoryClip);
            int elapsed = Math.Max(0, state.TerminalFrames - 1);
            int frame = actor.Identity == 1 ? elapsed % count : Math.Min(elapsed, count - 1);
            return (victoryClip, frame);
        }
        if (slot < 3 && Outcome == Ff7BattleOutcome.Escaped)
        {
            const int runClip = 7;
            return (runClip, Math.Max(0, state.TerminalFrames - 1) % animations!.ClipFrameCount(prefix, runClip));
        }
        if (slot < 3 && state.EscapePoseStarted is long start && CanAct(actor))
        {
            const int runClip = 7;
            return (runClip, (int)((PresentationTick - start) % animations!.ClipFrameCount(prefix, runClip)));
        }
        int clip = actor.Actor.IdleAction;
        int frames = animations!.ClipFrameCount(prefix, clip);
        if (state.ActorModels.Length == 0 && state.Reaction is { } reaction && reaction.Slot == slot)
        {
            int hurt = actor.Actor.HurtAction;
            int elapsed = (int)((Tick - reaction.Tick) / LogicUpdatesPerPresentation);
            if (elapsed < animations.ClipFrameCount(prefix, hurt))
            {
                return (hurt, elapsed);
            }
        }
        long idleFrame = modelState?.IdleStarted is long idleStarted ? PresentationTick - idleStarted : IdlePresentationFrame(slot);
        return (clip, (int)(idleFrame % frames));
    }

    public BattleLimitAnimation? VisualLimit(int slot)
    {
        ushort? limit = VisualLimitId(slot);
        return limit is ushort id ? animations!.GetLimit(id) : null;
    }

    private ushort? VisualLimitId(int slot)
    {
        Ff7BattleActorModelState? model = state.ActorModels.SingleOrDefault(model => model.Slot == slot);
        if (model is not null)
        {
            return model.Limit;
        }
        return state.Action is { Animation: >= 0, Command: { CommandId: (byte)Ff7BattleCommand.Limit } command } && command.ActorSlot == slot ? command.Argument : null;
    }

    private string ActorModel(int slot) => Ff7BattleAnimationResources.ModelPrefix(Actor(slot).Identity, slot >= 4);

    private Vector3 BonePosition(int slot, int bone)
    {
        Ff7BattleActorModelState? model = state.ActorModels.SingleOrDefault(model => model.Slot == slot);
        if (model?.Joints is { } joints && (uint)bone < joints.Length)
        {
            return joints[bone].ToVector();
        }
        return LiveBonePosition(slot, bone);
    }

    private Vector3 LiveBonePosition(int slot, int bone)
    {
        (int clip, int frame) = VisualPose(slot);
        if (VisualLimit(slot) is not null)
        {
            return animations!.LimitBonePosition(ActorModel(slot), clip, frame, VisualPlacement(slot), VisualLimitId(slot)!.Value, bone, Vector3.Zero, state.ActorModels.SingleOrDefault(model => model.Slot == slot)?.Modifiers?.PoseTransform);
        }
        return animations!.BonePosition(ActorModel(slot), clip, frame, VisualPlacement(slot), bone, state.ActorModels.SingleOrDefault(model => model.Slot == slot)?.Modifiers?.PoseTransform);
    }

    private Ff7BattlePlacement HomePlacement(int slot)
    {
        if (slot >= 4)
        {
            Vector3 position = Enemy(slot).Placement.Position;
            return new(position, Ff7BattlePlacementTables.EnemyYaw(Layout, position));
        }
        Ff7BattleActorState[] members = state.Actors.Where(actor => actor.Actor.Slot < 3).ToArray();
        int index = Array.FindIndex(members, member => member.Actor.Slot == slot);
        bool backRow = (members[index].Actor.Conditions & 0x40) != 0;
        return animations!.Placement.Party(Layout, members.Length, slot, index, backRow);
    }

    private Ff7BattleMotion? BeginApproach(Ff7BattleActionState action, int standoff, int steps, short[]? verticalSteps = null)
    {
        Ff7BattleQueuedCommand command = action.Command;
        Ff7BattleActorState[] targets = state.Actors.Where(target => (command.TargetMask & 1 << target.Actor.Slot) != 0).ToArray();
        if (targets.Length == 0 || steps == 0)
        {
            return action.Motion;
        }
        Ff7BattlePosition origin = action.Motion?.Position ?? Ff7BattlePosition.FromVector(VisualPlacement(command.ActorSlot).Position);
        Ff7BattlePosition center = default;
        int angle = 0;
        int radii = 0;
        foreach (Ff7BattleActorState target in targets)
        {
            Ff7BattlePlacement placement = HomePlacement(target.Actor.Slot);
            center += Ff7BattlePosition.FromVector(placement.Position);
            double direction = Math.Atan2(placement.Position.X - origin.X, placement.Position.Z - origin.Z);
            angle += (int)Math.Round(direction * 4096 / Math.Tau);
            radii += animations!.CollisionRadius(Ff7BattleAnimationResources.ModelPrefix(target.Identity, target.Actor.Slot >= 4));
        }
        center /= targets.Length;
        int distance = standoff + radii / targets.Length;
        int yaw = (0x800 - angle / targets.Length) & 0xFFF;
        double radians = yaw * Math.Tau / 4096;
        int sine = (int)Math.Round(Math.Sin(radians) * 4096);
        int cosine = (int)Math.Round(Math.Cos(radians) * 4096);
        Ff7BattlePosition destination = new(center.X - (sine * distance >> 12), targets.Length == 1 && command.ActorSlot >= 4 ? center.Y : origin.Y, center.Z + (cosine * distance >> 12));
        return new(origin, (destination - origin) / steps, steps, verticalSteps);
    }

    private int? TurnTowardTarget(Ff7BattleActionState action)
    {
        int source = action.Command.ActorSlot;
        int target = BitOperations.TrailingZeroCount((uint)action.Command.TargetMask);
        if (target >= Ff7NativeAtb.SlotCount || target == source)
        {
            return action.FacingYaw;
        }
        Vector3 start = action.Motion?.Position.ToVector() ?? HomePlacement(source).Position;
        Vector3 end = HomePlacement(target).Position;
        double direction = Math.Atan2(end.X - start.X, end.Z - start.Z);
        return ((int)Math.Round(direction * 4096 / Math.Tau) + 2048) & 4095;
    }
}
