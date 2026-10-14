namespace Memoria.FF8.Runtime;

/// <summary>Native475480 signed layer words, before common drawing-origin addition.</summary>
public static class NativeBackgroundLayerPlacement
{
    public static bool TryTranslation(int center, short offset, short scale, short current, out int translation)
    {
        translation = 0;
        if (center is < -4096 or > 4096)
        {
            return false;
        }
        int raw = center + offset - center * scale / 256 - current;
        if (raw is < -4096 or > 4095)
        {
            return false;
        }
        // This bounded domain preserves both original WORD narrowing and later eighth-pixel packing exactly.
        translation = raw;
        return true;
    }
}
