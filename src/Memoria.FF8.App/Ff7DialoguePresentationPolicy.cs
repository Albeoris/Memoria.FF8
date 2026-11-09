using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal static class Ff7DialoguePresentationPolicy
{
    internal static byte MessageSpeed(byte nativeSpeed, int sharedSpeed)
    {
        if (sharedSpeed is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(sharedSpeed));
        }
        return (byte)Math.Clamp(nativeSpeed + (3 - sharedSpeed) * 32, 0, 255);
    }

    internal static UiRect AnimatedPanel(PixelBounds panel, Ff7Dialogue dialogue, Ff7Window window)
    {
        if (window.Width <= 0 || window.Height <= 0)
        {
            throw new NotSupportedException("FFVII animated dialogue requires positive native target dimensions.");
        }
        Ff7DialogueBounds native = Ff7DialogueCursor.WindowBounds(dialogue, window);
        float width = panel.Width * (float)native.Width / window.Width;
        float height = panel.Height * (float)native.Height / window.Height;
        return new(panel.X + (panel.Width - width) / 2, panel.Y + (panel.Height - height) / 2, width, height);
    }

    internal static UiRect NativePanel(Ff7Dialogue dialogue, Ff7Window window, Rectangle bounds)
    {
        Ff7DialogueBounds native = Ff7DialogueCursor.WindowBounds(dialogue, window);
        return new(bounds.X + native.X * bounds.Width / 320f, bounds.Y + native.Y * bounds.Height / 240f, native.Width * bounds.Width / 320f, native.Height * bounds.Height / 240f);
    }

    internal static Rectangle Clip(UiRect panel, Rectangle bounds)
    {
        int left = (int)MathF.Ceiling(panel.X);
        int top = (int)MathF.Ceiling(panel.Y);
        int right = (int)MathF.Floor(panel.X + panel.Width);
        int bottom = (int)MathF.Floor(panel.Y + panel.Height);
        return Rectangle.Intersect(bounds, new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
    }
}
