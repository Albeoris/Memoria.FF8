using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7PlayerPointerBounds(float X, float Y, bool FlipHorizontal, bool FlipVertical);

public static class Ff7PlayerPointer
{
    public static bool IsRequested(byte helpMode, Ff7FieldProcess process)
    {
        return !process.CursorDisabled && (helpMode == 1 && !process.PlayerLocked || (helpMode & 2) != 0);
    }

    public static Ff7PlayerPointerBounds? Project(Ff7Actor actor, int fieldScale, PcFieldCamera camera, Vector2 cameraOffset)
    {
        Vector3 world = new(unchecked((short)((actor.X4096 >> 12) + actor.OffsetX)), unchecked((short)((actor.Y4096 >> 12) + actor.OffsetY)), unchecked((short)((actor.Z4096 >> 12) + actor.OffsetZ + ((fieldScale * 128) >> 9))));
        Vector3 projected = PcFieldProjection.Project(world, camera, Vector2.Zero);
        if (!float.IsFinite(projected.X) || !float.IsFinite(projected.Y))
        {
            return null;
        }
        int x = unchecked((short)(unchecked((short)(int)projected.X) + 160 + (short)cameraOffset.X));
        int y = unchecked((short)(unchecked((short)(int)projected.Y) + 104 + (short)cameraOffset.Y));
        return Place(x, y);
    }

    public static Ff7PlayerPointerBounds? Place(int x, int y)
    {
        if (x == short.MaxValue && y == short.MaxValue || x < -100 || y < -100)
        {
            return null;
        }
        x = Math.Clamp(x, 0, 320);
        y = Math.Clamp(y, -32, 224);
        bool flipHorizontal = x > 290;
        bool flipVertical = y < 17;
        return new(x - (flipHorizontal ? 15 : 0), y + 16 - (flipVertical ? 0 : 15), flipHorizontal, flipVertical);
    }
}
