using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleDestroyedSprite(Ff7BattleBraverPoint Position, int StartedAt, int Scale, int Variant);
public sealed record Ff7BattleDestroyedState(ushort TargetMask, int Frame = -1, IReadOnlyList<Ff7BattleDestroyedSprite>? Sprites = null)
{
    public bool Completed => Frame >= (BitOperations.PopCount(TargetMask) - 1) * 4 + 33;
    public byte ScreenBlue => Frame >= 0 && Frame < (BitOperations.PopCount(TargetMask) - 1) * 4 + 30 ? (byte)((Frame & 1) * 32 + 32) : (byte)0;
}

public sealed record Ff7BattleDestroyedUpdate(Ff7BattleDestroyedState State, ushort ImpactTargets);

public static class Ff7BattleDestroyedEffect
{
    public const int SpriteFrameCount = 12;

    public static Ff7BattleDestroyedState Start(ushort targetMask)
    {
        if (targetMask == 0 || (targetMask & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetMask));
        }
        return new(targetMask);
    }

    public static Ff7BattleDestroyedUpdate Advance(Ff7BattleDestroyedState state, Func<int, int> boneCount, Func<int, int, Vector3> bonePosition, Func<int> nextVisualRandom)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(boneCount);
        ArgumentNullException.ThrowIfNull(bonePosition);
        ArgumentNullException.ThrowIfNull(nextVisualRandom);
        if (state.Completed)
        {
            return new(state, 0);
        }
        int frame = state.Frame + 1;
        List<Ff7BattleDestroyedSprite> sprites = (state.Sprites ?? []).Where(sprite => frame - sprite.StartedAt < SpriteFrameCount).ToList();
        ushort impacts = 0;
        int ordinal = 0;
        for (int slot = 0; slot < 10; slot++)
        {
            if ((state.TargetMask & (1 << slot)) == 0)
            {
                continue;
            }
            int age = frame - ordinal++ * 4;
            if (age is >= 0 and < 22)
            {
                int bone = nextVisualRandom() % boneCount(slot);
                Ff7BattleBraverPoint point = Ff7BattleBraverPoint.At(bonePosition(slot, bone));
                short x = (short)(point.X + nextVisualRandom() % 400 - 200);
                short y = (short)(point.Y + nextVisualRandom() % 400 - 200);
                short z = (short)(point.Z + nextVisualRandom() % 400 - 200);
                int scale = 6144 + (nextVisualRandom() & 4095);
                int variant = nextVisualRandom() & 1;
                sprites.Add(new(new(x, y, z), frame, scale, variant));
            }
            if (age == 29)
            {
                impacts |= (ushort)(1 << slot);
            }
        }
        return new(state with { Frame = frame, Sprites = sprites }, impacts);
    }
}
