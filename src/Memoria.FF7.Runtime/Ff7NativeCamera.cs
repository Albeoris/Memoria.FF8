using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed class Ff7NativeCamera(PcFieldCamera camera, PcFieldTriggers triggers)
{
    public (short X, short Y) Follow(Ff7Actor actor, short focus)
    {
        Vector3 target = new(unchecked((short)(actor.X4096 >> 12)), unchecked((short)(actor.Y4096 >> 12)), unchecked((short)((actor.Z4096 >> 12) + focus)));
        Vector3 projected = PcFieldProjection.Project(target, camera, Vector2.Zero);
        if (!float.IsFinite(projected.X) || !float.IsFinite(projected.Y))
        {
            throw new NotSupportedException("FFVII camera target projects onto an undefined native screen coordinate.");
        }
        short x = unchecked((short)(int)projected.X);
        short y = unchecked((short)(int)projected.Y);
        x = unchecked((short)Math.Min(x, triggers.CameraRight - 160));
        x = unchecked((short)Math.Max(x, triggers.CameraLeft + 160));
        y = unchecked((short)Math.Min(y, triggers.CameraTop - 120));
        y = unchecked((short)Math.Max(y, triggers.CameraBottom + 120));
        if (triggers.CameraScrollConstraint is 1 or 2)
        {
            int startX = triggers.CameraLeft + 160;
            int startY = triggers.CameraScrollConstraint == 1 ? triggers.CameraBottom + 120 : triggers.CameraTop - 120;
            int dx = triggers.CameraRight - 160 - startX;
            int dy = (triggers.CameraScrollConstraint == 1 ? triggers.CameraTop - 120 : triggers.CameraBottom + 120) - startY;
            int numerator = unchecked((x - startX) * dx + (y - startY) * dy);
            int denominator = unchecked(dx * dx + dy * dy) >> 8;
            if (denominator == 0)
            {
                throw new InvalidDataException("FFVII constrained camera line has a zero native interpolation denominator.");
            }
            x = unchecked((short)((unchecked(numerator * dx) / denominator >> 8) + startX));
            y = unchecked((short)((unchecked(numerator * dy) / denominator >> 8) + startY));
        }
        return (unchecked((short)-x), unchecked((short)-y));
    }
}
