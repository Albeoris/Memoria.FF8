using System.Diagnostics;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private readonly PresentationFrameRate presentationFrameRate = new();
    private readonly long presentationClockStarted = Stopwatch.GetTimestamp();
    private TimeSpan nextFpsLabelUpdate;
    private string fpsLabel = "FPS: --";

    protected override void EndDraw()
    {
        base.EndDraw();
        TimeSpan now = Stopwatch.GetElapsedTime(presentationClockStarted);
        presentationFrameRate.Presented(now);
        if (now >= nextFpsLabelUpdate)
        {
            double? fps = presentationFrameRate.FramesPerSecond;
            fpsLabel = fps is double value ? FormattableString.Invariant($"FPS: {value:F1}") : "FPS: --";
            nextFpsLabelUpdate = now + TimeSpan.FromSeconds(0.25);
        }
    }

    private void DrawFps(Rectangle bounds)
    {
        if (!uiPreferences.ShowFps)
        {
            return;
        }
        float scale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        float margin = 8 * scale;
        float fontSize = 16 * scale;
        Vector2 size = text.MeasureWrapped(fpsLabel, fontSize, float.PositiveInfinity);
        Vector2 position = new(bounds.Right - size.X - margin * 2, bounds.Top + margin);
        Rectangle background = new((int)(position.X - margin), (int)position.Y, (int)MathF.Ceiling(size.X + margin * 2), (int)MathF.Ceiling(size.Y));
        batch.Draw(pixel, background, Color.Black * 0.75f);
        text.Draw(batch, fpsLabel, fontSize, position, Color.White);
    }
}
