namespace Memoria.FF8.Runtime;

/// <summary>45BC60's PC cropped texture rectangle; this is separate from a PSX per-pixel texture-window mask.</summary>
public sealed record NativeBattlePrimitiveTextureWindow(int X, int Y, int Width, int Height)
{
    public static NativeBattlePrimitiveTextureWindow Decode45BC60(uint bits)
    {
        bits &= 0xFFFFFF;
        int x = (int)((bits >> 7) & 0xF8);
        int y = (int)((bits >> 12) & 0xF8);
        int width = unchecked((byte)(~(bits << 3) + 1));
        int height = unchecked((byte)(~((bits >> 2) & 0xFFFFFFF8) + 1));
        if (width == 0)
        {
            width = 256;
        }
        if (height == 0)
        {
            height = 256;
        }
        return new(x, y, Math.Min(width, 256 - x), Math.Min(height, 256 - y));
    }
}
