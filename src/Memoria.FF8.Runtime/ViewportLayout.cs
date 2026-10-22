namespace Memoria.FF8.Runtime;

public readonly record struct PixelBounds(int X, int Y, int Width, int Height);

public static class ViewportLayout
{
    public static PixelBounds Fit(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Image and viewport dimensions must be positive.");
        }
        double scale = Math.Min((double)targetWidth / sourceWidth, (double)targetHeight / sourceHeight);
        int width = Math.Max(1, Math.Min(targetWidth, (int)Math.Round(sourceWidth * scale)));
        int height = Math.Max(1, Math.Min(targetHeight, (int)Math.Round(sourceHeight * scale)));
        return new PixelBounds((targetWidth - width) / 2, (targetHeight - height) / 2, width, height);
    }

    public static float UiScale(int viewportWidth, int viewportHeight)
    {
        return Math.Max(0.1f, Math.Min(viewportWidth / 960f, viewportHeight / 720f));
    }

    public static PixelBounds FieldRenderTarget(int viewportWidth, int viewportHeight, int longestSideLimit = 2048)
    {
        if (longestSideLimit is < 64 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(longestSideLimit));
        }
        PixelBounds fit = Fit(320, 240, viewportWidth, viewportHeight);
        int width = viewportWidth;
        int height = fit.Height;
        double scale = Math.Min(1, longestSideLimit / (double)Math.Max(width, height));
        return new PixelBounds(0, 0, Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    public static PixelBounds WorldRenderTarget(int viewportWidth, int viewportHeight, int longestSideLimit = 2048, bool worldMap = false)
    {
        if (longestSideLimit is < 64 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(longestSideLimit));
        }
        PixelBounds fit = Fit(320, worldMap ? 224 : 240, viewportWidth, viewportHeight);
        int width = viewportWidth;
        int height = worldMap ? fit.Height : viewportHeight;
        double scale = Math.Min(1, longestSideLimit / (double)Math.Max(width, height));
        return new PixelBounds(0, 0, Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }
}
