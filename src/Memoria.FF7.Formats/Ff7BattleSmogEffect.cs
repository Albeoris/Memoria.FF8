using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleSmogTarget(int Slot, Ff7BattleBraverPoint? Source = null, Ff7BattleBraverPoint? Velocity = null, Ff7BattleBraverPoint? Cloud = null);
public sealed record Ff7BattleSmogSprite(Ff7BattleBraverPoint Position, int StartedAt, int Variant, int Scale, Ff7BattleBraverPoint? Velocity = null);
public sealed record Ff7BattleSmogState(int Frame, IReadOnlyList<Ff7BattleSmogTarget> Targets, IReadOnlyList<Ff7BattleSmogSprite> Sprites)
{
    public bool Completed => Frame >= (Targets.Count - 1) * 4 + 49;
}

public sealed record Ff7BattleSmogUpdate(Ff7BattleSmogState State, ushort ImpactTargets);

public static class Ff7BattleSmogEffect
{
    public static Ff7BattleSmogState Start(ushort targetMask)
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
        int frame = state.Frame + 1;
        List<Ff7BattleSmogSprite> sprites = [];
        foreach (Ff7BattleSmogSprite sprite in state.Sprites)
        {
            if (frame - sprite.StartedAt >= (sprite.Velocity is null ? 8 : 12))
            {
                continue;
            }
            Ff7BattleBraverPoint position = sprite.Velocity is { } velocity ? new((short)(sprite.Position.X + velocity.X), (short)(sprite.Position.Y + velocity.Y), (short)(sprite.Position.Z + velocity.Z)) : sprite.Position;
            sprites.Add(sprite with { Position = position });
        }
        Ff7BattleSmogTarget[] targets = state.Targets.ToArray();
        ushort impacts = 0;
        int[] scales = [0x1000, 0x1800, 0x1400, 0x1100, 0x0980, 0x0600];
        for (int index = 0; index < targets.Length; index++)
        {
            int localFrame = frame - index * 4;
            if (localFrame < 0)
            {
                continue;
            }
            Ff7BattleSmogTarget target = targets[index];
            if (localFrame == 0)
            {
                Ff7BattleBraverPoint source = Ff7BattleBraverPoint.At(sourceAnchor());
                Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(targetAnchor(target.Slot));
                target = target with { Source = source, Velocity = new((short)((destination.X - source.X) / 12), (short)((destination.Y - source.Y) / 12), (short)((destination.Z - source.Z) / 12)) };
            }
            if (localFrame < 12 && localFrame % 4 != 3)
            {
                Ff7BattleBraverPoint source = target.Source ?? throw new InvalidDataException("FFVII Smog has no frozen source.");
                Ff7BattleBraverPoint velocity = target.Velocity ?? throw new InvalidDataException("FFVII Smog has no frozen velocity.");
                for (int spawn = 0; spawn < 2; spawn++)
                {
                    Ff7BattleBraverPoint position = new((short)(source.X - velocity.X / 2 * spawn), (short)(source.Y - velocity.Y / 2 * spawn), (short)(source.Z - velocity.Z / 2 * spawn));
                    sprites.Add(new(position, frame, localFrame % 4 * 2 + spawn, scales[localFrame % 4 * 2 + spawn] * 6 >> 2, velocity));
                }
            }
            if (localFrame == 12)
            {
                target = target with { Cloud = Ff7BattleBraverPoint.At(targetAnchor(target.Slot)) };
                impacts |= (ushort)(1 << target.Slot);
            }
            if (localFrame is >= 12 and < 42)
            {
                Ff7BattleBraverPoint cloud = target.Cloud ?? throw new InvalidDataException("FFVII Smog has no frozen cloud origin.");
                int age = localFrame - 12;
                int size = CloudSize(age) + (age < 10 ? 4 : -1);
                int angle = nextVisualRandom() & 4095;
                Vector3 offset = new(Cosine(angle) * size * 10 >> 12, Sine(angle) * size * 10 >> 12, 0);
                BattleRotationWords rotation = new(0, (short)(nextVisualRandom() & 4095), 0);
                Ff7BattleBraverPoint position = Ff7BattleBraverPoint.At(Vector3.Transform(offset, BattleRigidPose.Rotation(rotation)) + cloud.Position);
                sprites.Add(new(position, frame, nextVisualRandom() & 3, size * 200));
            }
            targets[index] = target;
        }
        return new(new(frame, Array.AsReadOnly(targets), sprites.AsReadOnly()), impacts);
    }

    public static int CloudSize(int frame) => Sine(frame * 0x44) * 40 >> 12;

    private static int Sine(int angle) => (int)(Math.Sin(angle * 0.087890625f * (Math.PI / 180)) * 4096);
    private static int Cosine(int angle) => (int)(Math.Cos(angle * 0.087890625f * (Math.PI / 180)) * 4096);
}
