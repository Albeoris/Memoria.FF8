using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleSmogTarget(int Slot, Ff7BattleBraverPoint? Source = null, Ff7BattleBraverPoint? Velocity = null, Ff7BattleBraverPoint? Cloud = null);
public sealed record Ff7BattleSmogSprite(Ff7BattleBraverPoint Position, int StartedAt, int Variant, int Scale, Ff7BattleBraverPoint? Velocity = null);
public sealed record Ff7BattleSmogState(int Frame, IReadOnlyList<Ff7BattleSmogTarget> Targets, IReadOnlyList<Ff7BattleSmogSprite> Sprites)
{
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= (Targets.Count - 1) * 4 + 49;
}

public sealed record Ff7BattleSmogUpdate(Ff7BattleSmogState State, ushort ImpactTargets);

public static class Ff7BattleSmogEffect
{
    public static Ff7BattleSmogState Start(ushort targetMask)
    {
        Ff7BattleSmogState state = CreateState(targetMask);
        return state with { Primary = Ff7BattlePrimaryEffects.ScheduleSmog(new(), 0, targetMask) };
    }

    internal static Ff7BattleSmogState CreateState(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        List<Ff7BattleSmogTarget> targets = [];
        for (int slot = 0; slot < 10; slot++)
        {
            if ((targetMask & (1 << slot)) != 0)
            {
                targets.Add(new(slot));
            }
        }
        return new(-1, targets.AsReadOnly(), []);
    }

    public static Ff7BattleSmogUpdate Advance(Ff7BattleSmogState state, Func<Vector3> sourceAnchor, Func<int, Vector3> targetAnchor, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sourceAnchor);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new NotSupportedException(), _ => default, _ => throw new NotSupportedException(), nextVisualRandom, JointPoint: (target, selector) => selector == 9 ? sourceAnchor() : targetAnchor(target));
        Ff7BattlePrimaryState primary = state.Primary ?? Ff7BattlePrimaryEffects.ImportSmog(new(), state, 0);
        primary = Ff7BattlePrimaryEffects.Advance(primary, context);
        return new(Ff7BattlePrimaryEffects.ProjectSmog(primary, state, retainPrimary: true), primary.ImpactTargets);
    }

    internal static Ff7BattleSmogSprite CreateCloudSprite(Ff7BattleBraverPoint cloud, int age, Func<int> nextVisualRandom)
    {
        int size = CloudSize(age) + (age < 10 ? 4 : -1);
        int angle = nextVisualRandom() & 4095;
        Vector3 offset = new(Cosine(angle) * size * 10 >> 12, Sine(angle) * size * 10 >> 12, 0);
        BattleRotationWords rotation = new(0, (short)(nextVisualRandom() & 4095), 0);
        Ff7BattleBraverPoint rotated = Ff7BattleBraverPoint.At(Vector3.Transform(offset, BattleRigidPose.Rotation(rotation)));
        Ff7BattleBraverPoint position = new((short)(rotated.X + cloud.X), (short)(rotated.Y + cloud.Y), (short)(rotated.Z + cloud.Z));
        return new(position, 0, nextVisualRandom() & 3, size * 200);
    }

    public static int CloudSize(int frame) => Sine(frame * 0x44) * 40 >> 12;

    private static int Sine(int angle) => (int)(Math.Sin(angle * 0.087890625f * (Math.PI / 180)) * 4096);
    private static int Cosine(int angle) => (int)(Math.Cos(angle * 0.087890625f * (Math.PI / 180)) * 4096);
}
