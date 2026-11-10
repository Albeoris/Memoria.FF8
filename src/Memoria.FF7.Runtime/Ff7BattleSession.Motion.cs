using System.Buffers.Binary;
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

public sealed record Ff7BattleMotion(Ff7BattlePosition Position, Ff7BattlePosition Step, int Remaining)
{
    public Ff7BattleMotion Advance()
    {
        return Remaining == 0 ? this : new(Position + Step, Step, Remaining - 1);
    }
}

public sealed partial class Ff7BattleSession
{
    public Ff7BattlePlacement VisualPlacement(int slot)
    {
        Ff7BattlePlacement home = HomePlacement(slot);
        return state.Action is { Motion: { } motion } action && action.Command.ActorSlot == slot ? home with { Position = motion.Position.ToVector() } : home;
    }

    public (int Clip, int Frame) VisualPose(int slot)
    {
        Ff7BattleActorState actor = Actor(slot);
        if (state.Action is { Pose: { ClipIndex: >= 0 } pose } action && action.Command.ActorSlot == slot)
        {
            return (pose.ClipIndex, pose.ClipFrame);
        }
        string prefix = Ff7BattleAnimationResources.ModelPrefix(actor.Identity, slot >= 4);
        int clip = actor.Actor.IdleAction;
        int frames = animations!.ClipFrameCount(prefix, clip);
        if (state.Reaction is { } reaction && reaction.Slot == slot)
        {
            int hurt = actor.Actor.HurtAction;
            int elapsed = (int)((Tick - reaction.Tick) / LogicUpdatesPerPresentation);
            if (elapsed < animations.ClipFrameCount(prefix, hurt))
            {
                return (hurt, elapsed);
            }
        }
        return (clip, (int)(PresentationTick % frames));
    }

    public BattleLimitAnimation? VisualLimit(int slot)
    {
        return state.Action is { Animation: >= 0, Command: { CommandId: (byte)Ff7BattleCommand.Limit } command } && command.ActorSlot == slot ? animations!.GetLimit(command.Argument) : null;
    }

    private string ActorModel(int slot) => Ff7BattleAnimationResources.ModelPrefix(Actor(slot).Identity, slot >= 4);

    private Vector3 BonePosition(int slot, int bone)
    {
        (int clip, int frame) = VisualPose(slot);
        return animations!.BonePosition(ActorModel(slot), clip, frame, VisualPlacement(slot), bone);
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

    private Ff7BattleMotion? BeginApproach(Ff7BattleActionState action, ReadOnlySpan<byte> operands)
    {
        Ff7BattleQueuedCommand command = action.Command;
        Ff7BattleActorState[] targets = state.Actors.Where(target => (command.TargetMask & 1 << target.Actor.Slot) != 0).ToArray();
        int steps = operands[4];
        if (targets.Length == 0 || steps == 0)
        {
            return action.Motion;
        }
        Ff7BattlePosition origin = action.Motion?.Position ?? Ff7BattlePosition.FromVector(HomePlacement(command.ActorSlot).Position);
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
        int distance = BinaryPrimitives.ReadUInt16LittleEndian(operands) + radii / targets.Length;
        int yaw = (0x800 - angle / targets.Length) & 0xFFF;
        double radians = yaw * Math.Tau / 4096;
        int sine = (int)Math.Round(Math.Sin(radians) * 4096);
        int cosine = (int)Math.Round(Math.Cos(radians) * 4096);
        Ff7BattlePosition destination = new(center.X - (sine * distance >> 12), targets.Length == 1 && command.ActorSlot >= 4 ? center.Y : origin.Y, center.Z + (cosine * distance >> 12));
        return new(origin, (destination - origin) / steps, steps);
    }
}
