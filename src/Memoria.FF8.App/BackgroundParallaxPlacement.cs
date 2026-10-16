using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Native475480 nonselected normal-layer WORD displacement plus common DRAWENV displacement.</summary>
internal static class BackgroundParallaxPlacement
{
    public static bool SupportsPan(FieldAssets field, int cameraIndex = 0)
    {
        Vector2 pan = field.GetCamera(cameraIndex).Pan;
        return float.IsFinite(pan.X) && float.IsFinite(pan.Y) && pan.X == MathF.Truncate(pan.X) && pan.Y == MathF.Truncate(pan.Y) && pan.X is >= -4095 and <= 4096 && pan.Y is >= -4095 and <= 4096 && (pan == Vector2.Zero || field.Information?.BackgroundWrapMask == 0);
    }

    public static bool SupportsDrawingOrigin(int x, int y, Vector2 pan)
    {
        // 476450 writes origin+PAN-C as WORDs;45C0F0 packs signed11-bit
        // E5 drawing offsets. Refuse an unproved wrapped drawing origin.
        float drawX = 160 + pan.X - x;
        float drawY = 112 + pan.Y - y;
        return drawX is >= -1024 and <= 1023 && drawY is >= -1024 and <= 1023;
    }

    public static bool SupportsCameraRange(FieldAssets field, BackgroundScrollSettings settings, short currentX = 0, short currentY = 0, int cameraIndex = 0)
    {
        if (field.Information is not { CameraBounds.Count: > 0 } information)
        {
            return false;
        }
        FieldBounds camera = information.CameraBounds[cameraIndex];
        int lowX = camera.Left + 160;
        int highX = Math.Max(lowX, camera.Right - 160);
        int lowY = camera.Top + 112;
        int highY = Math.Max(lowY, camera.Bottom - 112);
        // For any signed WORD scale, C - trunc(C*scale/256) is monotone. The two
        // original cap-then-floor clamp endpoints certify every normal C.
        return TryAxis(lowX, settings.OffsetX, settings.ScaleX, currentX, out _) && TryAxis(highX, settings.OffsetX, settings.ScaleX, currentX, out _) && TryAxis(lowY, settings.OffsetY, settings.ScaleY, currentY, out _) && TryAxis(highY, settings.OffsetY, settings.ScaleY, currentY, out _);
    }

    public static bool TryOffset(int x, int y, BackgroundScrollSettings settings, out Vector2 offset)
    {
        offset = default;
        if (!TryAxis(x, settings.OffsetX, settings.ScaleX, 0, out int offsetX) || !TryAxis(y, settings.OffsetY, settings.ScaleY, 0, out int offsetY))
        {
            return false;
        }
        offset = new Vector2(offsetX, offsetY);
        return true;
    }

    public static bool TryOffset(int x, int y, BackgroundScrollSettings settings, BackgroundScrollPosition? position, out Vector2 offset)
    {
        if (position is null)
        {
            // Normal475E60 has written zero current8/10 for this nonselected mode0 record.
            return TryOffset(x, y, settings, out offset);
        }
        offset = default;
        if (!NativeNonselectedBackgroundScrollCertificate.ValidPosition(position) || position.X is not short currentX || position.Y is not short currentY || !TryAxis(x, settings.OffsetX, settings.ScaleX, currentX, out int offsetX) || !TryAxis(y, settings.OffsetY, settings.ScaleY, currentY, out int offsetY))
        {
            return false;
        }
        offset = new Vector2(offsetX, offsetY);
        return true;
    }

    public static bool TryOffset(int x, int y, BackgroundScrollSettings settings, NativeActorBackgroundScroll actor, out Vector2 offset)
    {
        offset = default;
        if (!NativeActorScrollPhase.IsValid(actor) || actor.X is not short currentX || actor.Y is not short currentY || !TryAxis(x, settings.OffsetX, settings.ScaleX, currentX, out int offsetX) || !TryAxis(y, settings.OffsetY, settings.ScaleY, currentY, out int offsetY))
        {
            return false;
        }
        offset = new Vector2(offsetX, offsetY);
        return true;
    }

    private static bool TryAxis(int center, short origin, short scale, short current, out int offset)
    {
        offset = 0;
        if (!NativeBackgroundLayerPlacement.TryTranslation(center, origin, scale, current, out int raw))
        {
            return false;
        }
        // Native stores raw as WORD and later packs it in eighth units.
        // This domain proves both writes exact; unsupported wrapping refuses.
        offset = raw - center;
        return true;
    }
}
