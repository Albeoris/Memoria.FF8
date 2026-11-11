namespace Memoria.FF7.Formats;

internal static class Ff7BattleEffectMatrix
{
    public static Ff7BattleSearchScopeBasis RotateXyz(BattleRotationWords rotation)
    {
        (float sx, float cx) = Trig(rotation.X);
        (float sy, float cy) = Trig(rotation.Y);
        (float sz, float cz) = Trig(rotation.Z);
        float sxsy = (float)((double)sx * sy);
        float cxsy = (float)((double)cx * sy);
        return new(
            new(Word((float)((double)cy * cz)), Word((float)((double)sxsy * cz + (double)cx * sz)), Word((float)(-(double)cxsy * cz + (double)sx * sz))),
            new(Word((float)(-(double)cy * sz)), Word((float)(-(double)sxsy * sz + (double)cx * cz)), Word((float)((double)cxsy * sz + (double)sx * cz))),
            new(Word(sy), Word((float)(-(double)sx * cy)), Word((float)((double)cx * cy))));
    }

    public static Ff7BattleSearchScopeBasis RotateZyx(BattleRotationWords rotation)
    {
        (float sx, float cx) = Trig(rotation.X);
        (float sy, float cy) = Trig(rotation.Y);
        (float sz, float cz) = Trig(rotation.Z);
        float czsy = (float)((double)cz * sy);
        float szsy = (float)((double)sz * sy);
        return new(
            new(Word((float)((double)cz * cy)), Word((float)((double)sz * cy)), Word(-sy)),
            new(Word((float)((double)czsy * sx - (double)sz * cx)), Word((float)((double)szsy * sx + (double)cz * cx)), Word((float)((double)cy * sx))),
            new(Word((float)((double)czsy * cx + (double)sz * sx)), Word((float)((double)szsy * cx - (double)cz * sx)), Word((float)((double)cy * cx))));
    }

    public static Ff7BattleSearchScopeBasis Scale(Ff7BattleSearchScopeBasis basis, int x, int y, int z)
    {
        return new(Axis(basis.Right, x), Axis(basis.Up, y), Axis(basis.Forward, z));
    }

    public static Ff7BattleSearchScopeBasis Compose(Ff7BattleSearchScopeBasis local, Ff7BattleSearchScopeBasis world)
    {
        return new(Transform(local.Right, world), Transform(local.Up, world), Transform(local.Forward, world));
    }

    public static Ff7BattleBraverPoint Transform(Ff7BattleBraverPoint point, Ff7BattleSearchScopeBasis basis)
    {
        return new(
            (short)(((long)point.X * basis.Right.X + (long)point.Y * basis.Up.X + (long)point.Z * basis.Forward.X) / 4096),
            (short)(((long)point.X * basis.Right.Y + (long)point.Y * basis.Up.Y + (long)point.Z * basis.Forward.Y) / 4096),
            (short)(((long)point.X * basis.Right.Z + (long)point.Y * basis.Up.Z + (long)point.Z * basis.Forward.Z) / 4096));
    }

    public static Ff7BattleBraverPoint Add(Ff7BattleBraverPoint first, Ff7BattleBraverPoint second)
    {
        return new((short)(first.X + second.X), (short)(first.Y + second.Y), (short)(first.Z + second.Z));
    }

    private static Ff7BattleBraverPoint Axis(Ff7BattleBraverPoint point, int scale)
    {
        return new((short)((long)point.X * scale / 4096), (short)((long)point.Y * scale / 4096), (short)((long)point.Z * scale / 4096));
    }

    private static (float Sine, float Cosine) Trig(short angle)
    {
        double radians = angle * 0.087890625f * (Math.PI / 180);
        return ((float)Math.Sin(radians), (float)Math.Cos(radians));
    }

    private static short Word(float value)
    {
        return (short)(int)(value * 4096);
    }
}
