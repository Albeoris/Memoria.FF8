using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private void DrawFieldBars(Rectangle bounds, NativeFieldBarsState bars)
    {
        // TODO(TD-003): procedural field-bar rasterization.
        PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
        float scale = viewport.Width / 320f;
        foreach (NativeFieldBarDisplay bar in bars.VisibleBars)
        {
            UiRect frame = new(viewport.X + (bar.X + 8) * scale, viewport.Y + bar.Y * scale, bar.Width * scale, 16 * scale);
            uiCanvas!.Fill(frame, new UiColor(208, 208, 208));
            uiCanvas.Fill(frame.Inset(scale), new UiColor(24, 24, 32));
            UiRect fill = new(frame.X, frame.Y + 3 * scale, Math.Clamp((int)bar.Fill, 0, bar.Width) * scale, 8 * scale);
            uiCanvas.Fill(fill, bar.Flash ? new UiColor(240, 72, 48) : new UiColor(104, 200, 224));
        }
    }
}
