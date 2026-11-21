using System.Numerics;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    internal static Vector2 ApplyActorUvOffset(Vector2 original, ushort? packed)
    {
        if (packed is null or 0)
        {
            return original;
        }
        // Native nonzero mode transforms both original axes and subtracts one only once.
        const float scale = 1f / 256;
        float u = original.X + (packed.Value & 255) * scale;
        float v = original.Y + (packed.Value >> 8) * scale;
        if (u >= 1)
        {
            u -= 1;
        }
        if (v >= 1)
        {
            v -= 1;
        }
        return new(u, v);
    }
}
