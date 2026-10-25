using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    private static TextureSourceAddress? BattleModelSheetAddress(ResourceKey owner, TextureSourceRegion region, int textureCount)
    {
        string name = owner.Path;
        if (region.BitsPerPixel is not (4 or 8) || region.CanvasWidth != 128 || textureCount <= 0)
        {
            return null;
        }
        int row;
        int canvasHeight;
        if (name.Length == 17 && name.StartsWith("battle/c0m", StringComparison.Ordinal) && name.EndsWith(".dat", StringComparison.Ordinal) && AsciiDigits(name.AsSpan(10, 3)))
        {
            if (region.CanvasHeight != 128 || region.X is not (640 or 704 or 768) || region.Y is not (0 or 128))
            {
                return null;
            }
            row = (region.X - 640) / 64 * 2 + region.Y / 128;
            canvasHeight = 128;
        }
        else if (name.Length == 17 && name.StartsWith("battle/d", StringComparison.Ordinal) && char.IsAsciiHexDigit(name[8]) && AsciiDigits(name.AsSpan(10, 3)) && name.EndsWith(".dat", StringComparison.Ordinal))
        {
            if (name[9] == 'c')
            {
                if (region.Y < 0 || region.Y % 128 != 0 || region.CanvasHeight is <= 0 or > 128)
                {
                    return null;
                }
                row = region.Y / 128 & 1;
                canvasHeight = 128;
            }
            else if (name[9] == 'w' && textureCount == 1 && region.CanvasHeight == 32)
            {
                row = 0;
                canvasHeight = 32;
            }
            else
            {
                return null;
            }
        }
        else
        {
            return null;
        }
        return new TextureSourceAddress(owner, $"row/{row}", 0, 0, 0, region.CanvasWidth, region.CanvasHeight, 128, canvasHeight);
    }

    private static bool AsciiDigits(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }
        return true;
    }
}
