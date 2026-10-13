using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

/// <summary>Native475480 nonselected normal-layer WORD displacement plus common DRAWENV displacement.</summary>
internal static class BackgroundParallaxPlacement
{
    public static bool SupportsCameraRange(FieldAssets field, BackgroundScrollSettings settings)
    {
        if (settings.ScaleX is < 0 or > 256 || settings.ScaleY is < 0 or > 256 || field.Information is not { CameraBounds.Count: > 0 } information)
        {
            return false;
        }
        FieldBounds camera = information.CameraBounds[0];
        int lowX = camera.Left + 160;
        int highX = Math.Max(lowX, camera.Right - 160);
        int lowY = camera.Top + 112;
        int highY = Math.Max(lowY, camera.Bottom - 112);
        // For scale0..256, C - trunc(C*scale/256) is monotone. The two
        // original cap-then-floor clamp endpoints certify every normal C.
        return TryAxis(lowX, settings.OffsetX, settings.ScaleX, out _) && TryAxis(highX, settings.OffsetX, settings.ScaleX, out _) && TryAxis(lowY, settings.OffsetY, settings.ScaleY, out _) && TryAxis(highY, settings.OffsetY, settings.ScaleY, out _);
    }

    public static bool TryOffset(int x, int y, BackgroundScrollSettings settings, out Vector2 offset)
    {
        offset = default;
        if (!TryAxis(x, settings.OffsetX, settings.ScaleX, out int offsetX) || !TryAxis(y, settings.OffsetY, settings.ScaleY, out int offsetY))
        {
            return false;
        }
        offset = new Vector2(offsetX, offsetY);
        return true;
    }

    private static bool TryAxis(int center, short origin, short scale, out int offset)
    {
        offset = 0;
        if (center is < -4096 or > 4096 || scale is < 0 or > 256)
        {
            return false;
        }
        int quotient = center * scale / 256;
        int raw = center + origin - quotient;
        if (raw is < -4096 or > 4095)
        {
            return false;
        }
        // Native stores raw as WORD and later packs it in eighth units.
        // This domain proves both writes exact; unsupported wrapping refuses.
        offset = raw - center;
        return true;
    }
}
