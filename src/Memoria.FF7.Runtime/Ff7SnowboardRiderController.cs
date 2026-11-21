using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

internal sealed class Ff7SnowboardRiderController
{
    private const float AngleUnit = 0.0015339808305725455f;
    private readonly Ff7SnowboardNativeData native;
    private byte branchSelector;
    private byte reactionVariant;
    private short turnAmount;
    private short turnCounter;
    private short rotation;
    private short rotationDelta;
    private short rotationTicks;
    private uint random;
    private ushort held;
    private ushort pressed;
    private byte surfaceFlags;
    private byte attributes;
    private short courseDirection;
    private readonly List<Ff7SnowboardActionEffect> effects = [];

    internal Ff7SnowboardRiderController(Ff7SnowboardNativeData native, Vector3 position, uint seed)
    {
        this.native = native;
        random = seed;
        Motion = new() { Position = position, Gravity = 4096 };
        Script = new() { ActionWord = 16 };
    }

    internal Ff7SnowboardRiderMotionState Motion { get; }
    internal Ff7SnowboardActionScriptState Script { get; private set; }
    internal short ActionTick { get; private set; }
    internal float Heading { get; private set; }
    internal float Lean { get; private set; }
    internal Vector3 VisualAngles { get; private set; }
    internal int Impacts { get; private set; }
    internal uint RandomSeed => random;
    internal event Action<int, int, float>? PoseBlend;
    internal event Action<int>? PoseReference;

    internal void SetAction(ushort action)
    {
        Script = Script with { ActionWord = action };
        Motion.Action = unchecked((short)action);
    }

    internal IReadOnlyList<Ff7SnowboardActionEffect> Advance(ushort held, ushort pressed, long tick, byte surfaceFlags, byte attributes, short courseDirection)
    {
        this.held = held;
        this.pressed = pressed;
        this.surfaceFlags = surfaceFlags;
        this.attributes = attributes;
        this.courseDirection = courseDirection;
        effects.Clear();
        bool first = (Script.ActionWord & 0x8000) == 0;
        if (first)
        {
            Script = Ff7SnowboardActionScript.Reset(Script) with { ActionWord = (ushort)(Script.ActionWord | 0x8000) };
            Motion.Flags = 0;
            branchSelector = 0;
            ActionTick = 0;
        }
        Motion.Action = unchecked((short)Script.ActionWord);
        switch (Script.ActionWord & 255)
        {
            case 0:
            case 27:
            case 29:
            case 30:
            case 31:
                AdvanceScript();
                Motion.Flags |= 1;
                break;
            case 1:
            case 7:
                Normal();
                break;
            case 2:
                Push(first);
                break;
            case 3:
            case 4:
            case 5:
            case 6:
                Lean = 0;
                AdvanceScript();
                FollowHeading();
                break;
            case 10:
            case 12:
            case 14:
                Collision(first, tick);
                break;
            case 11:
                if (first)
                {
                    Lean = 0;
                    Impacts++;
                }
                SlowReaction();
                AdvanceScript();
                FollowHeading();
                break;
            case 13:
            case 15:
                if (first)
                {
                    branchSelector = reactionVariant;
                    Motion.Velocity = new(0, Motion.Velocity.Y, 0);
                    Motion.Flags |= 1;
                }
                AdvanceScript();
                if (Script.CanPush != 0)
                {
                    Heading = Ff7SnowboardSteering.ApproachAngle(courseDirection * AngleUnit, Heading, 0.02454369328916073f);
                }
                break;
            case 16:
                AdvanceScript();
                if (Script.CanPush != 0 && (held & 0x1000) != 0)
                {
                    SetAction(2);
                }
                break;
            case 17:
            case 18:
                Turn(first, (Script.ActionWord & 255) == 17);
                break;
            case 19:
            case 20:
            case 21:
            case 25:
                if (first)
                {
                    Motion.Velocity = new(0, Motion.Velocity.Y, 0);
                    Motion.Flags |= 1;
                }
                branchSelector = (byte)(((held & 0x1000) != 0) ? 1 : 0);
                AdvanceScript();
                break;
            case 22:
                Brake(first);
                break;
            case 23:
            case 24:
                Spin(first, (Script.ActionWord & 255) == 23);
                break;
            case 26:
                AdvanceScript();
                break;
            case 28:
                break;
            case 8:
            case 9:
                Skid(first);
                break;
            default:
                throw new NotSupportedException("The snowboard rider action has no native implementation.");
        }
        ActionTick = unchecked((short)(ActionTick + 1));
        Motion.Action = unchecked((short)Script.ActionWord);
        VisualAngles = VisualAngles with { Y = Lean };
        return effects.ToArray();
    }

    internal void UpdateVisualSlope(Vector3 normal)
    {
        float pitch = Motion.AirFrames > 8 ? 0 : (float)Math.Atan2(-normal.Z, normal.Y);
        float roll = Motion.AirFrames > 8 ? 0 : (float)Math.Atan2(normal.X, normal.Y);
        VisualAngles = new(Smooth(VisualAngles.X, pitch, 2), Lean, Smooth(VisualAngles.Z, roll, 3));

        static float Smooth(float current, float target, int shift)
        {
            int delta = (int)(((double)target - current) * 651.8986206054688f);
            if (delta == 0)
            {
                return current;
            }
            int step = Math.Max(1, Math.Abs(delta) >> shift);
            if (delta < 0)
            {
                step = -step;
            }
            return (float)((double)step * AngleUnit + current);
        }
    }

    internal void SignalLanding()
    {
        Script = Ff7SnowboardActionScript.SignalEvent(Script, 0);
    }

    private void AdvanceScript()
    {
        Ff7SnowboardActionScriptStep step = Ff7SnowboardActionScript.Advance(Script, native.ActionScripts, native.InputMasks, new(held, Motion.Speed, branchSelector));
        if (step.UnsupportedCommand is not null)
        {
            throw new NotSupportedException($"The snowboard rider action command {step.UnsupportedCommand} has no native implementation.");
        }
        Script = step.State;
        foreach (Ff7SnowboardActionEffect effect in step.Effects)
        {
            if (effect is Ff7SnowboardActionJump jump)
            {
                Motion.Velocity = new(Motion.Velocity.X, jump.VelocityY, Motion.Velocity.Z);
                Motion.Gravity = jump.Gravity;
                Motion.AirFrames = jump.AirFrames;
            }
            effects.Add(effect);
        }
    }

    private void FollowHeading()
    {
        Heading = Ff7SnowboardSteering.FollowVelocityHeading(Heading, Motion);
    }

    private void Normal()
    {
        Lean = 0;
        Motion.ResistanceShift = 0;
        branchSelector = (byte)(Motion.Speed < 256 ? 1 : 0);
        AdvanceScript();
        if ((pressed & 0x40) != 0 && Motion.AirFrames < 8)
        {
            SetAction(JumpAction());
        }
        else if ((held & 0x8000) != 0)
        {
            if (Motion.Speed > 32)
            {
                SetAction(17);
            }
            else
            {
                Heading -= 0.04908738657832146f;
            }
        }
        else if ((held & 0x2000) != 0)
        {
            if (Motion.Speed > 32)
            {
                SetAction(18);
            }
            else
            {
                Heading += 0.04908738657832146f;
            }
        }
        else if ((held & 0x4080) != 0 && Motion.Speed > 256)
        {
            SetAction(22);
        }
        else if (Motion.Speed < 256 && (held & 0x1000) != 0)
        {
            SetAction(2);
        }
        FollowHeading();
    }

    private ushort JumpAction()
    {
        if (Motion.Speed > 1024 && ((surfaceFlags & 8) != 0 || (attributes & 1) != 0))
        {
            return 6;
        }
        return (ushort)((held & 0x8000) != 0 ? 5 : (held & 0x2000) != 0 ? 4 : 3);
    }

    private void Push(bool first)
    {
        if (first)
        {
            Motion.ResistanceShift = 1;
        }
        if (Script.CanPush != 0)
        {
            Vector3 impulse = Rotate(new(0, 0, -12288), Heading);
            Motion.Velocity += impulse;
            Script = Script with { CanPush = unchecked((byte)(Script.CanPush - 1)) };
        }
        float angle = (32 - (Motion.Speed >> 4)) * AngleUnit;
        angle = (held & 0x8000) != 0 ? -angle : (held & 0x2000) != 0 ? angle : 0;
        if (angle != 0)
        {
            Heading += angle;
            Motion.Velocity = Rotate(Motion.Velocity, angle);
        }
        branchSelector = (byte)(Motion.Speed < 256 && (held & 0x1000) != 0 ? 0 : 1);
        AdvanceScript();
        if ((Script.ActionWord & 255) != 2)
        {
            Motion.ResistanceShift = 0;
        }
    }

    private void Turn(bool first, bool left)
    {
        if (first)
        {
            turnAmount = 0;
            turnCounter = 0;
            Motion.ResistanceShift = 0;
            PoseReference?.Invoke(Pose(0));
        }
        int target = (held & (left ? 0x8000 : 0x2000)) != 0 ? ((held & 12) != 0 ? 128 : 32) : 0;
        int step = (held & 12) != 0 ? 2 : 1;
        if (target == 0)
        {
            step *= 2;
        }
        if (turnAmount < target)
        {
            turnAmount = unchecked((short)Math.Min(target, turnAmount + step));
        }
        else if (turnAmount > target)
        {
            turnAmount = unchecked((short)Math.Max(0, turnAmount - step));
        }
        Lean = turnAmount > 32 ? ((turnAmount - 32) * (left ? -8 : 8)) * AngleUnit : 0;
        PoseBlend?.Invoke(-1, Pose(left ? 2 : 3), Math.Min(turnAmount, (short)32) / 32f);
        if (turnAmount > 32)
        {
            turnCounter = unchecked((short)(turnCounter + (turnAmount >> 5)));
        }
        else if (turnCounter != 0)
        {
            turnCounter--;
        }
        Ff7SnowboardSteering.Turn(Motion, left ? -turnAmount : turnAmount);
        FollowHeading();
        if (Motion.AirFrames == 0 && 140 - (unchecked(Motion.Speed * Motion.Speed) >> 16) < turnAmount)
        {
            SetAction((ushort)(left ? 23 : 24));
        }
        else if ((pressed & 0x40) != 0 && Motion.AirFrames < 8)
        {
            SetAction(JumpAction());
        }
        else if (turnAmount == 0)
        {
            SetAction((ushort)((held & (left ? 0x2000 : 0x8000)) != 0 ? (left ? 18 : 17) : 1));
        }
        else if ((held & 0x4080) != 0 && Motion.Speed > 256)
        {
            SetAction(22);
        }
        else if (Motion.Speed < 256 && (held & 0x1000) != 0)
        {
            SetAction(2);
        }
    }

    private void Brake(bool first)
    {
        if (first)
        {
            rotation = 0;
            turnAmount = 0;
            Motion.ResistanceShift = 4;
        }
        int target = (held & 0x4080) != 0 && rotation == 0 ? 32 : 0;
        if (target > turnAmount)
        {
            turnAmount = unchecked((short)(turnAmount + 2));
        }
        else if (target < turnAmount)
        {
            turnAmount = unchecked((short)Math.Max(0, turnAmount - 2));
        }
        float fraction = -NativeCosine((turnAmount << 5) + 2048) / 4096f;
        PoseBlend?.Invoke(Pose(1), Pose(4), fraction);
        if (Motion.AirFrames == 0)
        {
            Motion.Velocity = new(Motion.Velocity.X - Motion.Velocity.X * 0.03125f, Motion.Velocity.Y, Motion.Velocity.Z - Motion.Velocity.Z * 0.03125f);
            FollowHeading();
        }
        if (turnAmount == 0)
        {
            SetAction(1);
            Motion.ResistanceShift = 0;
        }
    }

    private void Spin(bool first, bool left)
    {
        if (first)
        {
            rotation = (short)(left ? -128 : 128);
            rotationDelta = 0;
            rotationTicks = 64;
            Lean = 0;
            Motion.ResistanceShift = 0;
            Impacts++;
        }
        Ff7SnowboardSteering.Turn(Motion, -turnAmount);
        if (Motion.Speed < 512)
        {
            rotationTicks = 0;
        }
        if (rotationTicks != 0)
        {
            if (rotation != rotationDelta)
            {
                rotationDelta = unchecked((short)(rotationDelta + (rotation >> 3)));
            }
            rotationTicks--;
        }
        else if (rotationDelta != 0)
        {
            rotationDelta = unchecked((short)(rotationDelta - (rotation >> 1)));
        }
        else
        {
            Script = Ff7SnowboardActionScript.SignalEvent(Script, 1);
        }
        Heading += rotationDelta * AngleUnit;
        AdvanceScript();
    }

    private void Skid(bool first)
    {
        if (first)
        {
            rotation = (short)((Script.ActionWord & 255) == 8 ? -24 : 24);
            Lean = 0;
            Motion.Velocity = new(Motion.Velocity.X * 0.5f, Motion.Velocity.Y, Motion.Velocity.Z * 0.5f);
            Motion.ResistanceShift = 0;
            Impacts++;
        }
        int amount = (held & 0x8000) != 0 ? -((NextRandom() & 15) + 4) : (held & 0x2000) != 0 ? (NextRandom() & 15) + 4 : 0;
        amount = unchecked((short)(amount + rotation));
        Motion.Velocity = Rotate(Motion.Velocity, amount * AngleUnit);
        if (rotation != 0)
        {
            rotation = unchecked((short)(rotation + (rotation > 0 ? -2 : 2)));
        }
        AdvanceScript();
        FollowHeading();
    }

    private void Collision(bool first, long tick)
    {
        if (first)
        {
            float factor = (Script.ActionWord & 255) == 12 ? -0.5f : 0.5f;
            Motion.Velocity = new(Motion.Velocity.X * factor, Motion.Velocity.Y, Motion.Velocity.Z * factor);
            Lean = 0;
            Motion.ResistanceShift = 0;
            Impacts += 2;
            reactionVariant = Motion.Speed > 1280 ? (byte)((ushort)tick % 3 + 1) : (byte)0;
        }
        if (Script.CanPush != 0)
        {
            SlowReaction();
        }
        AdvanceScript();
        FollowHeading();
    }

    private void SlowReaction()
    {
        if (Motion.AirFrames > 4)
        {
            return;
        }
        Motion.Velocity = new(Motion.Velocity.X - Motion.Velocity.X * 0.03125f, Motion.Velocity.Y, Motion.Velocity.Z - Motion.Velocity.Z * 0.03125f);
        if (Motion.Speed < 384)
        {
            Motion.ResistanceShift = 3;
        }
        if (!(branchSelector == 0 && Motion.Speed < 256) && ActionTick <= 240)
        {
            return;
        }
        Motion.ResistanceShift = 0;
        branchSelector = 1;
    }

    internal int NextRandom()
    {
        random = unchecked(random * 214013 + 2531011);
        return (int)(random >> 16) & 32767;
    }

    private static short NativeCosine(int angle)
    {
        double radians = angle * (double)(float)Math.PI / 2048;
        return unchecked((short)(int)(Math.Cos(radians) * 4096));
    }

    private int Pose(int index)
    {
        if (native.ActionPoses.Count != 5)
        {
            throw new InvalidDataException("The snowboard rider action requires its native pose selection table.");
        }
        return native.ActionPoses[index];
    }

    private static Vector3 Rotate(Vector3 vector, float angle)
    {
        float sine = (float)Math.Sin(angle);
        float cosine = (float)Math.Cos(angle);
        return new((float)((double)cosine * vector.X + (double)sine * vector.Z), vector.Y, (float)(-(double)sine * vector.X + (double)cosine * vector.Z));
    }
}
