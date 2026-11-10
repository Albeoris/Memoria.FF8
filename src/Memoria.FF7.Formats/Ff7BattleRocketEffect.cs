using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleRocketProjectile(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, short Yaw, short Lift = 50);
public sealed record Ff7BattleRocketTarget(int Slot, int StartedAt, Ff7BattleRocketProjectile? Projectile = null);
public sealed record Ff7BattleRocketExplosion(Ff7BattleBraverPoint Position, int StartedAt);
public sealed record Ff7BattleRocketSprite(Ff7BattleBraverPoint Position, int Kind, int Frame);
public sealed record Ff7BattleRocketState(int Frame, IReadOnlyList<Ff7BattleRocketTarget> Targets, IReadOnlyList<Ff7BattleRocketExplosion> Explosions)
{
    public IReadOnlyList<Ff7BattleRocketProjectile> Missiles { get; init; } = [];
    public IReadOnlyList<Ff7BattleRocketSprite> Sprites { get; init; } = [];
    public IReadOnlyList<Ff7BattleBraverPoint> Points { get; init; } = [];
    public bool Completed => Frame >= 40 + (Targets.Count - 1) * 4;
}

public sealed record Ff7BattleRocketUpdate(Ff7BattleRocketState State, ushort ImpactTargets);

public static class Ff7BattleRocketEffect
{
    public const int ExhaustFrameCount = 3;
    public const int SmokeFrameCount = 6;
    public const int ExplosionFrameCount = 16;

    public static Ff7BattleRocketState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        List<Ff7BattleRocketTarget> targets = [];
        for (int slot = 0; slot < 10; slot++)
        {
            if ((targetMask & (1 << slot)) != 0)
            {
                targets.Add(new(slot, targets.Count * 4));
            }
        }
        return new(-1, targets, []);
    }

    public static Ff7BattleRocketUpdate Advance(Ff7BattleRocketState state, Func<Vector3> sourceAnchor, Func<int, Vector3> targetAnchor, Func<int> nextCrt)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sourceAnchor);
        ArgumentNullException.ThrowIfNull(targetAnchor);
        ArgumentNullException.ThrowIfNull(nextCrt);
        if (state.Completed)
        {
            return new(state, 0);
        }
        int frame = state.Frame + 1;
        Ff7BattleRocketTarget[] targets = state.Targets.ToArray();
        List<Ff7BattleRocketExplosion> explosions = state.Explosions.Where(explosion => frame - explosion.StartedAt < ExplosionFrameCount).ToList();
        List<Ff7BattleRocketProjectile> missiles = [];
        List<Ff7BattleRocketSprite> sprites = [];
        List<Ff7BattleBraverPoint> points = [];
        ushort impacts = 0;
        for (int index = 0; index < targets.Length; index++)
        {
            Ff7BattleRocketTarget target = targets[index];
            int age = frame - target.StartedAt;
            if (age < 0)
            {
                continue;
            }
            if (age == 0)
            {
                Ff7BattleBraverPoint source = Ff7BattleBraverPoint.At(sourceAnchor());
                Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(targetAnchor(target.Slot));
                Ff7BattleBraverPoint velocity = new((short)((destination.X - source.X) / 15), (short)((destination.Y - source.Y) / 15), (short)((destination.Z - source.Z) / 15));
                short yaw = (short)(int)((double)(float)Math.Atan2(destination.X - source.X, destination.Z - source.Z) * (180 / Math.PI) * 11.377778f);
                target = target with { Projectile = new(source, velocity, yaw) };
            }
            if (age == 20)
            {
                impacts |= (ushort)(1 << target.Slot);
            }
            if (age is > 20 and < 24)
            {
                for (int spawn = 0; spawn < 2; spawn++)
                {
                    Ff7BattleBraverPoint anchor = Ff7BattleBraverPoint.At(targetAnchor(target.Slot));
                    explosions.Add(new(new((short)(anchor.X + nextCrt() % 1000 - 500), (short)(anchor.Y + nextCrt() % 1000 - 500), (short)(anchor.Z + nextCrt() % 1000 - 500)), frame));
                }
            }
            if (age < 20)
            {
                Ff7BattleRocketProjectile missile = target.Projectile ?? throw new InvalidDataException("FFVII rocket continuation has no original projectile state.");
                missiles.Add(missile);
                Ff7BattleBraverPoint tailOffset = Ff7BattleBraverPoint.At(Vector3.Transform(new(0, 0, -100), YawMatrix(missile.Yaw)));
                Ff7BattleBraverPoint tail = new((short)(missile.Position.X + tailOffset.X), (short)(missile.Position.Y + tailOffset.Y), (short)(missile.Position.Z + tailOffset.Z));
                sprites.Add(new(tail, 0, age % ExhaustFrameCount));
                if (age >= 6)
                {
                    int distance = 80 + (age & 1) * 80;
                    short x = (short)(-Sine(missile.Yaw) * distance >> 12);
                    short z = (short)(-Cosine(missile.Yaw) * distance >> 12);
                    Ff7BattleBraverPoint smoke = tail;
                    for (int particle = 0; particle < 12; particle++)
                    {
                        sprites.Add(new(smoke, 1, particle >> 1));
                        smoke = smoke with { X = (short)(smoke.X + x), Z = (short)(smoke.Z + z) };
                    }
                }
                for (int particle = 0; particle < 10; particle++)
                {
                    points.Add(new((short)(tail.X + nextCrt() % 400 - 200), (short)(tail.Y + nextCrt() % 400 - 200), (short)(tail.Z + nextCrt() % 400 - 200)));
                }
                Ff7BattleBraverPoint position = missile.Position;
                if (age < 6)
                {
                    position = new((short)(position.X + (missile.Velocity.X >> 2)), (short)(position.Y + (missile.Velocity.Y >> 2) + missile.Lift), (short)(position.Z + (missile.Velocity.Z >> 2)));
                }
                else
                {
                    position = new((short)(position.X + missile.Velocity.X), (short)(position.Y + missile.Velocity.Y - 10), (short)(position.Z + missile.Velocity.Z));
                }
                target = target with { Projectile = missile with { Position = position, Lift = age < 6 ? (short)(missile.Lift - 5) : missile.Lift } };
            }
            targets[index] = target;
        }
        foreach (Ff7BattleRocketExplosion explosion in explosions)
        {
            sprites.Add(new(explosion.Position, 2, frame - explosion.StartedAt));
        }
        return new(new(frame, targets, explosions) { Missiles = missiles, Sprites = sprites, Points = points }, impacts);
    }

    private static int Sine(int angle) => (int)(Math.Sin(angle * 0.087890625f * (Math.PI / 180)) * 4096);
    private static int Cosine(int angle) => (int)(Math.Cos(angle * 0.087890625f * (Math.PI / 180)) * 4096);

    public static Matrix4x4 YawMatrix(short yaw)
    {
        float sine = Sine(yaw) / 4096f;
        float cosine = Cosine(yaw) / 4096f;
        return new(cosine, 0, -sine, 0, 0, 1, 0, 0, sine, 0, cosine, 0, 0, 0, 0, 1);
    }
}
