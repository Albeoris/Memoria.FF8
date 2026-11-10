using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeBattleCameraGeometry
{
    public static Ff7BattleCameraPoint Position(Ff7BattleCameraPoint joint, Ff7BattleCameraPoint offset, Ff7BattleCameraModelParameters model, short scaleControl, bool inverted, short facing, Ff7BattleCameraMath math)
    {
        int x = Scale(offset.X, model.ScaleX);
        int y = Scale(offset.Y, model.ScaleY);
        int z = Scale(offset.Z, model.ScaleZ);
        if (inverted)
        {
            int square = unchecked(x * x + z * z);
            if (square < 0)
            {
                throw new NotSupportedException("FFVII camera offset with an overflowing native squared radius is unsupported.");
            }
            int radius = (int)Math.Sqrt(square);
            int angle = unchecked(math.Angle(x, z) + facing);
            x = unchecked(math.Sine(angle) * radius) >> 12;
            z = unchecked(math.Cosine(angle) * radius) >> 12;
        }
        return new(unchecked((short)(joint.X + x)), unchecked((short)(joint.Y + y)), unchecked((short)(joint.Z + z)));

        int Scale(short value, ushort axis)
        {
            int factor = scaleControl < 0x1000 ? Math.Max(0x100, axis / 2) : axis;
            return unchecked(value * factor) >> 8;
        }
    }

    public static Ff7BattleCameraPoint Focus(Ff7BattleCameraPoint joint, Ff7BattleCameraPoint offset, short modelHeight, short actionAnimation, bool useModelHeight)
    {
        int y = useModelHeight && !(actionAnimation == 1 && modelHeight > 0) ? unchecked(offset.Y + modelHeight) : offset.Y;
        return new(unchecked((short)(joint.X + offset.X)), unchecked((short)(joint.Y + y)), unchecked((short)(joint.Z + offset.Z)));
    }
}
